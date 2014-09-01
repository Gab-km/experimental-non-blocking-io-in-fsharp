﻿namespace AsyncFrameworkSample

type Try<'T> =
  | Success of 'T
  | Failure of System.Exception

type Future<'T>(f: (unit -> 'T)) =
    let mutable result : 'T option = None
    let mutable whenCompleted : (Try<'T> -> unit) option = None
    do
        let agent = MailboxProcessor<(unit -> 'T) * AsyncReplyChannel<'T>>.Start(fun inbox ->
            let rec loop n =
                async {
                    let! (body, replyChannel) = inbox.Receive()
                    replyChannel.Reply(body())
                    return! loop (n + 1)
                }
            loop 0)
        let messageAsync = agent.PostAndAsyncReply(fun replyChannel -> f, replyChannel)
        Async.StartWithContinuations(messageAsync,
            (fun reply -> result <- Some(reply)
                          match whenCompleted with
                          | Some(delg) -> delg (Success(reply))
                          | None    -> ()),
            (fun exn   -> match whenCompleted with
                          | Some(delg) -> delg (Failure(exn))
                          | None    -> ()),
            (fun _     -> ()))
    member self.OnComplete(f) = whenCompleted <- Some(f)    // 処理完了後に OnComplete を設定した場合はどうあるべきか？
    [<System.Obsolete>]member self.Value = f()              // おそらく今後なくなる
    member self.Result = result

module Future = begin
  let get (f: Future<'T>) = f.Value
end

type Server<'Msg>(f: ('Msg -> unit)) =
    member self.Listen(address:string, port:int) = ()
    member self.Start() = ()

type Client() =
    let client = new System.Net.Sockets.TcpClient()
    interface System.IDisposable with
        member self.Dispose() = if client <> null then client.Close()
    member self.Connect(address:string, port:int) = client.Connect(address, port)
    member private self.postWithNetworkStream(message: byte[], ns: System.Net.Sockets.NetworkStream) =
        try
            let writeAsync() = async {
                    ns.Write(message, 0, message.Length)
                }
            writeAsync()
            |> Async.RunSynchronously
            |> Success
        with
            | ex -> Failure(ex)
    member self.Post(message: byte[]) =
        use ns = client.GetStream()
        self.postWithNetworkStream(message, ns)
    member self.PostAndReply(message: byte[]) =
        use ns = client.GetStream()
        let writeResult = self.postWithNetworkStream(message, ns)
        match writeResult with
        | Success(_) ->
            try
                let received = Array.init 256 (fun _ ->0uy)
                ns.Read(received, 0, received.Length)
                |> ignore
                Success(received)
            with
                | ex -> Failure(ex)
        | Failure(ex) -> Failure(ex)
    member self.PostAndAsyncReply(message: byte[]) =
        use ns = client.GetStream()
        let writeResult = self.postWithNetworkStream(message, ns)
        match writeResult with
        | Success(_) ->
            try
                Future(fun () ->
                    let received = Array.init 256 (fun _ ->0uy)
                    ns.Read(received, 0, received.Length) |> ignore
                    received)
                |> Success
            with
                | ex -> Failure(ex)
        | Failure(ex) -> Failure(ex)
