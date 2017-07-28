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

```fsharp
    let socketExample = socketIO {
        do! writeSocket "What is your name?"
        let! name = readSocket
        do! writeSocket ("Hello " + name)
    }
    Socket.run socketExample 9000 |> Async.RunSynchronously
```

These small snippets can be composed as well!
```fsharp
    let socketExample = socketIO {
        do! writeSocket "What is your name?"
        return! readSocket
    }

    let socketComposed = socketIO {
        let! name = socketExample
        do! writeSocket("Your name is " + name)
    }

    Socket.run socketComposed 9000 |> Async.RunSynchronously
```
