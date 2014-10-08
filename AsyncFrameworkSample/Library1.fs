namespace AsyncFrameworkSample

type Try<'T> =
  | Success of 'T
  | Failure of System.Exception

type TryingBuilder() =
    member self.Bind(x, f) =
        match x with
        | Success(value) ->
            try
                f value
            with
                | ex -> Failure(ex)
        | Failure(ex) -> Failure(ex)
    member self.Return(x) = Success(x)

type Future<'T>(f: (unit -> 'T)) =
    // 成功と失敗の結果をフィールドに持つべきか？
    let mutable value : Try<'T> option = None
    let mutable whenCompleted : (Try<'T> -> unit) = fun t -> ()
    do
        use agent = new MailboxProcessor<(unit -> 'T) * AsyncReplyChannel<'T>>(fun inbox ->
            let rec loop n =
                async {
                    let! (body, replyChannel) = inbox.Receive()
                    replyChannel.Reply(body())
                    return! loop (n + 1)
                }
            loop 0)
        agent.Error.Add(fun error -> value <- Some(Failure(error)); whenCompleted <| Failure(error))
        agent.Start()
        let messageAsync = agent.PostAndAsyncReply(fun replyChannel -> f, replyChannel)
        Async.StartWithContinuations(messageAsync,
            (fun reply -> value <- Some(Success(reply))
                          whenCompleted <| Success(reply)),
            (fun _     -> ()),
            (fun _     -> printfn "cancelled."))
    member self.OnComplete(f) = whenCompleted <- f          // イベントに書きなおそうかな。
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

  let trying = TryingBuilder()

end

open Future

type Server<'Msg>(f: ('Msg -> unit)) =
    member self.Listen(address:string, port:int) = ()
    member self.Start() = ()

type Client() =
    let client = new System.Net.Sockets.TcpClient()
    interface System.IDisposable with
        member self.Dispose() = if client <> null then client.Close()
    member self.Connect(address:string, port:int) = client.Connect(address, port)
    member private self.postWithNetworkStream(message: byte[], ns: System.Net.Sockets.NetworkStream) =
        trying {
            let writeAsync() = async {
                    ns.Write(message, 0, message.Length)
                }
            let result = writeAsync() |> Async.RunSynchronously
            return result
        }
    member self.Post(message: byte[]) =
        use ns = client.GetStream()
        self.postWithNetworkStream(message, ns)
    member self.PostAndReply(message: byte[]) =
        use ns = client.GetStream()
        trying {
            let! writeResult = self.postWithNetworkStream(message, ns)
            
            let received = Array.init 256 (fun _ -> 0uy)
            ns.Read(received, 0, received.Length) |> ignore
            return received
        }
    member self.PostAndAsyncReply(message: byte[]) =
        use ns = client.GetStream()
        trying {
            let! writeResult = self.postWithNetworkStream(message, ns)
            let f = Future(fun () ->
                        let received = Array.init 256 (fun _ ->0uy)
                        ns.Read(received, 0, received.Length) |> ignore
                        received)
            return f
        }
