namespace AsyncFrameworkSample

type Try<'T> =
  | Success of 'T
  | Failure of System.Exception

type Future<'T> =
  class
    new : (unit -> 'T) -> Future<'T>
    member OnComplete : f:(Try<'T> -> unit) -> unit
    member Result : 'T option
  end

module Future = begin
  val get : Future<'T> -> 'T    // Is it needed?
end

type Server<'Msg> =
  class
    new : ('Msg -> unit) -> Server<'Msg>
    member Listen : address:string * port:int -> unit
    member Start : unit -> unit
  end

type Client =
  class
    interface System.IDisposable
    new : unit -> Client
    member Connect : address:string * port:int -> unit
    member Post : byte[] -> unit
    member PostAndReply : byte[] -> byte[]
    member PostAndAsyncReply : byte[] -> Future<byte[]>
  end