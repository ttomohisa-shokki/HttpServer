#! /bin/bash

cat application.info

exec dotnet HttpServer.dll "$@"
