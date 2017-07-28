namespace Socks

open System
open System.Net
open System.Net.Sockets
open System.Text

type SocketIO<'a> = 
    | Write of (Socket -> string * 'a * Socket)
    | Read of (Socket -> (string -> 'a) * Socket)

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
                let msg, a, socket = g socket 
                msg, f a, socket)
        | Read(g) ->
            Read(fun socket ->
                let h, socket = g socket
                h >> f, socket)


    let rec bind (f: 'a -> FreeSocket<'b>) (free: FreeSocket<'a>): FreeSocket<'b> =
        match free with
        | Pure v -> f v
        | FreeSocket io -> FreeSocket (map (bind f) io)

    let liftF (io: SocketIO<'a>): FreeSocket<'a> =
        FreeSocket (map Pure io)

    let private initSocket (port: int) = 
        let ipHostInfo = Dns.GetHostEntryAsync(Dns.GetHostName()) |> Async.AwaitTask |> Async.RunSynchronously
        let ipAddress = ipHostInfo.AddressList.[0]
        let localEndPoint = IPEndPoint(ipAddress, port)
        printfn "Listening at address %s on port %d" (ipAddress.ToString()) port
        let socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        socket.Bind(localEndPoint)
        socket.Listen(100)
        socket

    let run (s: FreeSocket<'a>) (port: int) : 'a =
        // Set up the state
        let listener = initSocket port
        let socket = listener.Accept()
        printfn "Socket Connected!"
        let rec helper s socket = 
            match s with
            | FreeSocket(Write (writeFn)) -> 
                let msg, next, socket = writeFn socket
                socket.Send(Encoding.ASCII.GetBytes(msg + "\n")) |> ignore
                helper next socket
            | FreeSocket(Read(readFn)) ->
                let readFn, socket = readFn socket
                let mutable buffer = Array.zeroCreate<byte>(1024)
                let read = socket.Receive(buffer)
                let msg = Encoding.ASCII.GetString(buffer, 0, read)
                helper (readFn msg) socket
            | Pure a -> 
                socket.Shutdown(SocketShutdown.Both)
                socket.Dispose()
                a
        helper s socket

[<AutoOpen>]
module SocketExtensions =
    type SocketBuilder() = 
        member __.Bind(free, f) = Socket.bind f free 
        member __.Return(v) = Pure v
        member __.Combine(f1, f2) = Socket.bind (fun _ -> f2) f1
        member __.Zero() = Pure ()
        member __.Delay(f) = f()
    
    
    let writeSocket s = Socket.liftF(Write(fun socket -> s, (), socket))
    let readSocket = Socket.liftF(Read(fun socket -> id, socket))
    let socketIO = SocketBuilder()