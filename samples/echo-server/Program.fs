open Socks
open System.Threading


[<EntryPoint>]
let main argv =
    let rec echoServer () = socketIO {
        let! line = Socket.readSocket
        do! Socket.writeSocket (line)
        return! echoServer ()
    }
    printfn "Starting Server..."
    Socket.runParallel 9000 100 (echoServer()) |> Async.RunSynchronously
    0
