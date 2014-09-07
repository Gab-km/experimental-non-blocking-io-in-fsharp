#load "Library1.fs"
open AsyncFrameworkSample

// helper function
let printFuture = function
| Some(t) ->
    match t with
    | Success(result) -> printfn "Success: %A" result
    | Failure(ex)     -> printfn "Failure: %s" ex.Message
| None    -> printfn "Not completed."

// `f.Value` has `Some (Success 5)`
let f = Future(fun () -> 5)

// 3 seconds later, `g.Value` has `Some (Success 3)`.
let g = Future(fun () -> Async.Sleep 3000 |> Async.RunSynchronously; 3)

// TODO: Failure pattern will not work.
let h = Future(fun () -> failwith "bad condition"; 6)
