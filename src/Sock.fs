namespace Socks

open System
open System.Net
open System.Net.Sockets
open System.Text

type SocketIO<'a> = 
    | Write of (Socket -> Async<string * 'a * Socket>)
    | Read of (Socket -> Async<(string -> 'a) * Socket>)

type FreeSocket<'a> =
    | Pure of 'a
    | FreeSocket of SocketIO<FreeSocket<'a>>

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


    let rec bind (f: 'a -> FreeSocket<'b>) (free: FreeSocket<'a>): FreeSocket<'b> =
        match free with
        | Pure v -> f v
        | FreeSocket io -> FreeSocket (map (bind f) io)

    let liftF (io: SocketIO<'a>): FreeSocket<'a> =
        FreeSocket (map Pure io)

    let private initSocket (port: int) = 
        async {
            let! ipHostInfo = Dns.GetHostEntryAsync(Dns.GetHostName()) |> Async.AwaitTask
            let ipAddress = ipHostInfo.AddressList.[0]
            let localEndPoint = IPEndPoint(ipAddress, port)
            printfn "Listening at address %s on port %d" (ipAddress.ToString()) port
            let socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            do socket.Bind(localEndPoint)
            do socket.Listen(100)
            return socket
        }

    let run (free: FreeSocket<'a>) (port: int) : Async<'a> =
        // Set up the state
        let rec helper s socket = 
            async {
                match s with
                | FreeSocket(Write (writeFn)) -> 
                    let! msg, next, socket = writeFn socket
                    do socket.Send(Encoding.ASCII.GetBytes(msg + "\n")) |> ignore
                    return! helper next socket
                | FreeSocket(Read(readFn)) ->
                    let! readFn, socket = readFn socket
                    let mutable buffer = Array.zeroCreate<byte>(1024)
                    let read = socket.Receive(buffer)
                    let msg = Encoding.ASCII.GetString(buffer, 0, read)
                    return! helper (readFn msg) socket
                | Pure a -> 
                    socket.Shutdown(SocketShutdown.Both)
                    socket.Dispose()
                    return a
            }
            
        async {
            let! listener = initSocket port
            let! socket = listener.AcceptAsync() |> Async.AwaitTask
            return! helper free socket
        }

[<AutoOpen>]
module SocketExtensions =
    type SocketBuilder() = 
        member __.Bind(free, f) = Socket.bind f free 
        member __.Return(v) = Pure v
        member __.Combine(f1, f2) = Socket.bind (fun _ -> f2) f1
        member __.Zero() = Pure ()
        member __.Delay(f) = f()
    
    
    let writeSocket s = Socket.liftF(Write(fun socket -> async { return s, (), socket }))
    let readSocket = Socket.liftF(Read(fun socket -> async { return id, socket }))
    let socketIO = SocketBuilder()