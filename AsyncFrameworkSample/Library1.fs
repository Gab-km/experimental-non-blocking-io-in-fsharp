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
                          | Some(f) -> f (Success(reply))
                          | None    -> ()),
            (fun exn   -> match whenCompleted with
                          | Some(f) -> f (Failure(exn))
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
    let client = new System.Net.Sockets.TcpClient()     // 安全に破棄する方法を追加すること
    member self.Connect(address:string, port:int) = client.Connect(address, port)
    member self.Post(message: byte[]) =
        use ns = client.GetStream()
        if ns.CanWrite
            then ns.BeginWrite(message, 0, message.Length, (fun _ -> ()), ns) |> ignore
    member self.PostAndReply(message: byte[]) = message
    member self.PostAndAsyncReply(message: byte[]) =
        use ns = client.GetStream()
        Future(fun () -> message)