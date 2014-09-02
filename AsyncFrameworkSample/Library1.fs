namespace AsyncFrameworkSample

type Try<'T> =
  | Success of 'T
  | Failure of System.Exception

type Future<'T>(f: (unit -> 'T)) =
    // 成功と失敗の結果をフィールドに持つべきか？
    let mutable value : Try<'T> option = None
    let mutable whenCompleted : (Try<'T> -> unit) = fun t -> ()
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
            (fun reply -> value <- Some(Success(reply))
                          whenCompleted <| Success(reply)),
            (fun exn   -> value <- Some(Failure(exn))
                          whenCompleted <| Failure(exn)),
            (fun _     -> ()))
    member self.OnComplete(f) = whenCompleted <- f          // 処理完了後に OnComplete を設定した場合はどうあるべきか？
    member self.Value = value

module Future = begin
  let map (f: 'T -> 'U) (ft: Future<'T>) =
    match ft.Value with
    | Some(result) ->
        match result with
        | Success(v) -> Future(fun () -> f v)
        | Failure(e) -> Future(fun () -> raise e)
    | None         -> Future(fun () ->
                        let gen: (unit -> 'U) ref = ref (fun () -> failwith "")     // ここの実装は良くない
                        ft.OnComplete (function
                            | Success(v) -> gen := (fun () -> f v)
                            | Failure(e) -> gen := (fun () -> raise e))
                        () |> !gen)
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
