namespace Socks

open System
open System.Net
open System.Net.Sockets
open System.Text

type SocketIO<'a> = 
    | Write of (NetworkStream -> Async<string * 'a * NetworkStream>)
    | Read of (NetworkStream -> Async<(string -> 'a) * NetworkStream>)

type Socket<'a> =
    | Pure of 'a
    | Socket of SocketIO<Socket<'a>>

[<RequireQualifiedAccess>]
module Socket =


    // Define our functor
    let map (f: 'a -> 'b) (io: SocketIO<'a>) : SocketIO<'b> =
        match io with
        | Write(g) -> 
            Write(fun socket ->
                async {
                    let! msg, a, socket = g socket 
                    return msg, f a, socket
                })
        | Read(g) ->
            Read(fun socket ->
                async {
                    let! h, socket = g socket
                    return h >> f, socket
                })


    let rec bind (f: 'a -> Socket<'b>) (free: Socket<'a>): Socket<'b> =
        match free with
        | Pure v -> f v
        | Socket io -> Socket (map (bind f) io)

    let liftF (io: SocketIO<'a>): Socket<'a> =
        Socket (map Pure io)

    let private initServer (port: int) (maxConnections: int) = 
        async {
            try
                let! ipHostInfo = Dns.GetHostEntryAsync(Dns.GetHostName()) |> Async.AwaitTask
                let ipAddress = ipHostInfo.AddressList.[0]
                let localEndPoint = IPEndPoint(ipAddress, port)
                let listener = TcpListener(localEndPoint)
                listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1)
                listener.Start(maxConnections)
                return listener 
            with e -> printfn "Server Init failed: %s" e.Message; return null
        }
    
    let rec private interpret s stream = 
        async {
            match s with
            | Socket(Write (writeFn)) -> 
                let! msg, next, stream = writeFn stream
                do! stream.WriteAsync(Encoding.ASCII.GetBytes(msg), 0, msg.Length) |> Async.AwaitTask
                return! interpret next stream
            | Socket(Read(readFn)) ->
                let! readFn, stream = readFn stream
                let mutable buffer = Array.zeroCreate<byte>(1024)
                let! read = stream.ReadAsync(buffer, 0, buffer.Length) |> Async.AwaitTask
                let msg = Encoding.ASCII.GetString(buffer, 0, read)
                return! interpret (readFn msg) stream
            | Pure a -> 
                stream.Dispose()
                return a
        }
    /// Runs a socket computation for a single connection
    let run (port: int) (free: Socket<'a>) : Async<'a> =
        async {
            let! listener = initServer port 1
            let! tcp = listener.AcceptTcpClientAsync() |> Async.AwaitTask
            let! res = interpret free (tcp.GetStream())
            do tcp.Dispose()
            do listener.Stop()
            return res
        }

    /// Runs a socket computation as a parallel connection, 
    let runParallel (port: int) (maxConnections: int) (free: Socket<'a>) : Async<unit> =
        let handleClient (tcp: TcpClient) = async {
            let! res = interpret free (tcp.GetStream())
            do tcp.Dispose()
        }
        let rec loop (listener: TcpListener) = async {
            let! tcp = listener.AcceptTcpClientAsync() |> Async.AwaitTask
            do handleClient tcp |> Async.Start
            return! loop listener
        } 
        async {
            let! listener = initServer port maxConnections
            let! res = loop listener
            do listener.Stop()
        }

    let writeSocket s = liftF(Write(fun socket -> async { return s, (), socket }))
    let readSocket = liftF(Read(fun socket -> async { return id, socket }))

[<AutoOpen>]
module SocketExtensions =
    type SocketBuilder() = 
        member __.Bind(free, f) = Socket.bind f free 
        member __.Return(v) = Pure v
        member __.ReturnFrom(v: Socket<_>) = v
        member __.Combine(f1, f2) = Socket.bind (fun _ -> f2) f1
        member __.Zero() = Pure ()
        member __.Delay(f) = f()
    
    
    let socketIO = SocketBuilder()