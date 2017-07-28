// Learn more about F# at http://fsharp.org
open Socks
open System.Threading


[<EntryPoint>]
let main argv =
    let socketExample = socketIO {
        do! Socket.writeSocket "What is your name?"
        let! name = Socket.readSocket
        do! Socket.writeSocket ("Hello " + name)
    }
    Async.Start(Socket.runParallel 9000 100 socketExample)
    printfn "Sleeping main thread..."
    Thread.Sleep(60*1000)
    printfn "Bye!"
    0
