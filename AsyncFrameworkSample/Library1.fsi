namespace AsyncFrameworkSample

type Try<'T> =
  | Success of 'T
  | Failure of System.Exception

type Future<'T> =
  class
    new : (unit -> 'T) -> Future<'T>
    member OnComplete : f:(Try<'T> -> unit) -> unit
    member Value : Try<'T> option
  end

module Future = begin
  val map : ('T -> 'U) -> Future<'T> -> Future<'U>
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
    // 以下のメソッド、それぞれの戻り値が Try 型で本当にいいのか？
    member Post : byte[] -> Try<unit>
    member PostAndReply : byte[] -> Try<byte[]>
    member PostAndAsyncReply : byte[] -> Try<Future<byte[]>>
  end