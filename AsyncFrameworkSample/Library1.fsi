namespace AsyncFrameworkSample

type Try<'T> =
  | Success of 'T
  | Failure of System.Exception

type Future<'T> =
  class
    new : unit -> Future<'T>
    member OnComplete<'U, 'Result> : f:(Try<'U> -> 'Result) -> unit
  end

type Server<'msg> =
  class
    new : ('msg -> unit) -> Server<'msg>
    member Listen : address:string * port:int -> unit
    member Start : unit -> unit
  end