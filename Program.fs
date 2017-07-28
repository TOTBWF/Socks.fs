// Learn more about F# at http://fsharp.org
open System
open System.Net
open System.Net.Sockets
open System.Text

type SocketIO<'a> = 
    // | Close of (Socket -> 'a)
    | Write of (Socket -> string * 'a * Socket)
    | Read of (Socket -> (string -> 'a) * Socket)

type FreeSocket<'a> =
    | Pure of 'a
    | FreeSocket of SocketIO<FreeSocket<'a>>

module Socket =


    // Define our functor
    let map (f: 'a -> 'b) (io: SocketIO<'a>) : SocketIO<'b> =
        match io with
        // | Close(g) -> Close(g >> f)
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
    let (>>=) = fun free f -> Socket.bind f free
    let (>>.) = fun f1 f2 -> f1 >>= fun _ -> f2
    type SocketBuilder() = 
        member __.Bind(free, f) = free >>= f
        member __.Return(v) = Pure v
        member __.Combine(f1, f2) = f1 >>. f2
        member __.Zero() = Pure ()
        member __.Delay(f) = f()
    
    
    let write s = Socket.liftF(Write(fun socket -> s, (), socket))
    let read = Socket.liftF(Read(fun socket -> id, socket))
    let socketIO = SocketBuilder()


[<EntryPoint>]
let main argv =
    let socketExample = socketIO {
        do! write "What is your name?"
        let! name = read 
        do! write ("Hello " + name)
    }
    Socket.run socketExample 9000
    0
