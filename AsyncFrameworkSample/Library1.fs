namespace AsyncFrameworkSample

type Try<'T> =
  | Success of 'T
  | Failure of System.Exception

type private Delg<'T> = unit -> 'T

type Future<'T>(f: (unit -> 'T)) =
    let mutable result : 'T option = None
    do
        let agent = MailboxProcessor<Delg<'T> * AsyncReplyChannel<'T>>.Start(fun inbox ->
            let rec loop n =
                async {
                    let! (delg, replyChannel) = inbox.Receive()
                    replyChannel.Reply(delg())
                    return! loop (n + 1)
                }
            loop 0)
        let messageAsync = agent.PostAndAsyncReply(fun replyChannel -> f, replyChannel)
        Async.StartWithContinuations(messageAsync,
            (fun reply -> result <- Some(reply)),
            (fun exn   -> ()),
            (fun _     -> ()))
    member self.OnComplete<'U, 'Result>(f:(Try<'U> -> 'Result)) = ()
    member self.Value = f()
    member self.Result = result

module Future = begin
  let get (f: Future<'T>) = f.Value
end

type Server<'Msg>(f: ('Msg -> unit)) =
    member self.Listen(address:string, port:int) = ()
    member self.Start() = ()

type Client<'Msg>(f: (Client<'Msg> -> unit)) =
    member self.Connect(address:string, port:int) = ()
    member self.Post(message: 'Msg) = ()
    member self.PostAndReply(message: 'Msg) = message
    member self.PostAndAsyncReply(message: 'Msg) = Future(fun () -> message)