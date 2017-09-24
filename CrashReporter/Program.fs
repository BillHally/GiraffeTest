module CrashReporter.App

open System
open System.Diagnostics
open System.IO
open System.Collections.Generic

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Hosting.WindowsServices

open Giraffe.HttpHandlers
open Giraffe.Middleware
open Giraffe.Razor.HttpHandlers
open Giraffe.Razor.Middleware
open Giraffe.Tasks
open Giraffe.HttpHandlers
open Giraffe.HttpContextExtensions

// ---------------------------------
// Web app
// ---------------------------------
[<CLIMutable>]
type CrashReport =
    {
        Application : string
        StackTrace  : string
        Comments    : string
    }

type CrashReportResponse =
    {
        Response : string
        Report : CrashReport
    }

let crashReportHandler =
    fun (next : HttpFunc) (ctx : HttpContext) ->
        task {
            let! x = ctx.BindJson<CrashReport>()

            return! json { Response = "OK"; Report = x } next ctx
        }

let webApp =
    choose [
        // GET >=>
        //     choose [
        //         route "/" >=> razorHtmlView "Index" { Text = "Hello world, from Giraffe!" }
        //     ]

        POST >=> choose [
                route "/" >=> crashReportHandler        
            ]        
        setStatusCode 404 >=> text "Not Found" ]

// ---------------------------------
// Error handler
// ---------------------------------

let errorHandler (ex : Exception) (logger : ILogger) =
    logger.LogError(EventId(), ex, "An unhandled exception has occurred while executing the request.")
    clearResponse >=> setStatusCode 500 >=> text ex.Message

// ---------------------------------
// Config and Main
// ---------------------------------

let configureApp (app : IApplicationBuilder) =
    app.UseGiraffeErrorHandler errorHandler
    app.UseStaticFiles() |> ignore
    app.UseGiraffe webApp

let configureServices (services : IServiceCollection) =
    let sp  = services.BuildServiceProvider()
    let env = sp.GetService<IHostingEnvironment>()
    let viewsFolderPath = Path.Combine(env.ContentRootPath, "Views")
    services.AddRazorEngine viewsFolderPath |> ignore

let configureLogging (builder : ILoggingBuilder) =
    let filter (l : LogLevel) = l.Equals LogLevel.Error
    builder.AddFilter(filter).AddConsole().AddDebug() |> ignore

[<EntryPoint>]
let main argv =

    let pathToAssembly = System.Reflection.Assembly.GetExecutingAssembly().Location
    let contentRoot = Path.GetDirectoryName(pathToAssembly)
    let webRoot     = Path.Combine(contentRoot, "WebRoot")
    
    let host =
        WebHostBuilder()
            .UseKestrel()
            .UseContentRoot(contentRoot)
            .UseIISIntegration()
            .UseWebRoot(webRoot)
            .Configure(Action<IApplicationBuilder> configureApp)
            .ConfigureServices(configureServices)
            .ConfigureLogging(configureLogging)
            .Build()

    if Debugger.IsAttached || Environment.UserInteractive then
        printfn "Running as a console app..."        
        host.Run()
    else
        host.RunAsService()
    
    0
