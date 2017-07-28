open Socks
open System.Threading


[<EntryPoint>]
let main argv =
    let rec echoServer () = socketIO {
        let! line = Socket.readSocket
        if line.Trim() <> "QUIT"  then
            do! Socket.writeSocket (line)
            return! echoServer ()
        else
            do! Socket.writeSocket ("Goodbye!\n")
            return ()
    }
    printfn "Starting Server..."
    Socket.runParallel 9000 100 (echoServer()) |> Async.RunSynchronously
    0
