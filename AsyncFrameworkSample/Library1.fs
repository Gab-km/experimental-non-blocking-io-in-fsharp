namespace AsyncFrameworkSample

type Try<'T> =
  | Success of 'T
  | Failure of System.Exception

type Future<'T>(f: (unit -> 'T)) =
    member self.OnComplete<'U, 'Result>(f:(Try<'U> -> 'Result)) = ()
    member self.Value = f()

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