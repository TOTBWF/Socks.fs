// Learn more about F# at http://fsharp.org
open Socks


[<EntryPoint>]
let main argv =
    let socketExample = socketIO {
        do! writeSocket "What is your name?"
        let! name = readSocket
        do! writeSocket ("Hello " + name)
    }
    Socket.run socketExample 9000 |> Async.RunSynchronously
    0
