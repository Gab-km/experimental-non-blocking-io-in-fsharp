namespace AsyncFrameworkSample

type Try<'T> =
  | Success of 'T
  | Failure of System.Exception

type Future<'T> =
  class
    new : unit -> Future<'T>
    member OnComplete<'U, 'Result> : f:(Try<'U> -> 'Result) -> unit
  end

module Future = begin
  val get : Future<'T> -> 'T
end

type Server<'Msg> =
  class
    new : ('Msg -> unit) -> Server<'Msg>
    member Listen : address:string * port:int -> unit
    member Start : unit -> unit
  end

type Client<'Msg> =
  class
    new : (Client<'Msg> -> unit) -> Client<'Msg>
    member Connect : address:string * port:int -> unit
    member Post : 'Msg -> unit
    member PostAndReply : 'Msg -> 'Msg
    member PostAndAsyncReply : 'Msg -> Future<'Msg>
  end