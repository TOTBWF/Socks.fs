// Learn more about F# at http://fsharp.org
open System
open System.Net
open System.Net.Sockets
open System.Text

type SocketIO<'a> = 
    | Connect of int * (Socket -> 'a)
    | Close of Socket * 'a
    | Write of Socket * string * 'a
    | Read of Socket * (string -> 'a)

type FreeSocket<'a> =
    | Pure of 'a
    | FreeSocket of SocketIO<FreeSocket<'a>>

module Socket =

    // Define our functor
    let map (f: 'a -> 'b) (io: SocketIO<'a>) : SocketIO<'b> =
        match io with
        | Connect(p, fn) -> Connect(p, fn >> f)
        | Close(sock, v) -> Close(sock, f v)
        | Write(sock, s, next) -> Write(sock, s, f next)
        | Read(sock, fn) -> Read(sock, fn >> f)

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

    let run (s: FreeSocket<'a>) : 'a =
        // Set up the state
        let rec helper s = 
            match s with
            | FreeSocket(Connect(port, f)) ->
                printfn "Waiting for connection..."
                let listener = initSocket port
                let socket = listener.Accept()
                printfn "Socket Connected!"
                helper (f socket) 
            | FreeSocket(Close(socket, v)) ->
                printfn "Closing socket..."
                socket.Shutdown(SocketShutdown.Both)
                socket.Dispose()
                helper v
            | FreeSocket(Write (socket, s, next)) -> 
                printfn "Writing socket: %s" s
                let res = socket.Send(Encoding.ASCII.GetBytes(s + "\n"))
                printfn "Message sent with result: %d" res
                helper next 
            | FreeSocket(Read(socket, f)) ->
                // Do Socket IO instead
                printfn "Blocking On Read..."
                let mutable buffer = Array.zeroCreate<byte>(1024)
                let read = socket.Receive(buffer)
                let msg = Encoding.ASCII.GetString(buffer, 0, read)
                helper (f msg) 
            | Pure a -> a
        helper s 

[<AutoOpen>]
module SocketExtensions =
    let (>>=) = fun free f -> Socket.bind f free
    let (>>.) = fun f1 f2 -> f1 >>= fun _ -> f2
    type SocketBuilder() = 
        member __.Bind(free, f) = free >>= f
        member __.Return(v) = Pure v
        member __.Combine(f1, f2) = f1 >>. f2
        member __.Zero() = Pure ()
        member __.Delay(f) = f()
    
    
    let connect port = Socket.liftF(Connect(port, id))
    let write socket s = Socket.liftF(Write(socket, s, ()))
    let read socket = Socket.liftF(Read(socket, id))
    let close socket = Socket.liftF(Close(socket, ()))
    let socketIO = SocketBuilder()


[<EntryPoint>]
let main argv =
    let socketExample = socketIO {
        let! socket = connect 9000
        do! write socket "What is your name?"
        let! name = read socket
        do! write socket ("Hello " + name)
        do! close socket
    }
    Socket.run socketExample
    0
