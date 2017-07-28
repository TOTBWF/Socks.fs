# Socks.fs
A functional socket library for F#

Socks is a library for F# that provides a simple, safe, and composable method for socket programming.

## Getting Started

First, install and build the library
```sh
cd src
dotnet restore
dotnet build
```

Now all we have to do is use the `socketIO` computation expression and we are off to the races!
Notice how all of the socket computation is being run asynchrounously!

```fsharp
    let socketExample = socketIO {
        do! Socket.writeSocket "What is your name?"
        let! name = Socket.readSocket
        do! Socke.twriteSocket ("Hello " + name)
    }
    Socket.run 9000 socketExample |> Async.Start
```

These small snippets can be composed as well!
```fsharp
    let socketExample = socketIO {
        do! Socket.writeSocket "What is your name?"
        return! Socket.readSocket
    }

    let socketComposed = socketIO {
        let! name = Socket.socketExample
        do! Socket.writeSocket("Your name is " + name)
    }

    Socket.run 9000 socketComposed |> Async.Start
```

There is also the option to run the same computation with multiple connections:
```fsharp
    let socketExample = socketIO {
        do! Socket.writeSocket "What is your name?"
        let! name = Socket.readSocket
        do! Socket.writeSocket ("Hello " + name)
    }
    Async.Start(Socket.runParallel 9000 100 socketExample)
```

