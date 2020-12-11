// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#r @"packages/FAKE/tools/FakeLib.dll"
open Fake.Core
open Fake.Core.TargetOperators
open Fake.DotNet
open Fake.DotNet.NuGet.NuGet
open Fake.IO
open Fake.IO.Globbing.Operators
open System
open System.IO


Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
let (++) x y = Path.Combine(x,y)
// --------------------------------------------------------------------------------------
// START TODO: Provide project-specific details below
// --------------------------------------------------------------------------------------

// Information about the project are used
//  - for version and project name in generated AssemblyInfo file
//  - by the generated NuGet package
//  - to run tests and to publish documentation on GitHub gh-pages
//  - for documentation, you also need to edit info in "docs/tools/generate.fsx"

// The name of the project
// (used by attributes in AssemblyInfo, name of a NuGet package and directory in 'src')
let project = "XrmContext"
let projectPath = "src" ++ project

// Short summary of the project
// (used as description in AssemblyInfo and as a short summary for NuGet package)
let summary = "Tool to generate early-bound .NET classes and enumerations for MS Dynamics 365/CRM server-side coding."

let company = "Delegate A/S"
let copyright = @"Copyright (c) Delegate A/S 2017"

// File system information 
let solutionFile  = "XrmContext.sln"

// --------------------------------------------------------------------------------------
// END TODO: The rest of the file includes standard build steps
// --------------------------------------------------------------------------------------

// Read additional information from the release notes document
let release = ReleaseNotes.parse (File.ReadAllLines "RELEASE_NOTES.md")

// Helper active pattern for project types
let (|Fsproj|Csproj|Vbproj|) (projFileName:string) = 
    match projFileName with
    | f when f.EndsWith("fsproj") -> Fsproj
    | f when f.EndsWith("csproj") -> Csproj
    | f when f.EndsWith("vbproj") -> Vbproj
    | _                           -> failwith (sprintf "Project file %s not supported. Unknown project type." projFileName)

// Generate assembly info files with the right version & up-to-date information
Target.create "AssemblyInfo" (fun _ ->
  let fileName = projectPath + "/AssemblyInfo.fs"
  AssemblyInfoFile.createFSharp fileName
    [ 
      AssemblyInfo.Title project
      AssemblyInfo.Product project
      AssemblyInfo.Description summary
      AssemblyInfo.Company company
      AssemblyInfo.Copyright copyright
      AssemblyInfo.Version release.AssemblyVersion
      AssemblyInfo.FileVersion release.AssemblyVersion
    ]
)

// --------------------------------------------------------------------------------------
// Setting up VS for building with FAKE
let commonBuild solution =
  let packArgs (defaults:MSBuild.CliArguments) = 
    { defaults with
        NoWarn = Some(["NU5100"])
        Properties = 
        [
          "Version", release.NugetVersion
          "ReleaseNotes", String.Join(Environment.NewLine, release.Notes)
        ] 
    }
  solution
  |> DotNet.build (fun buildOp -> 
    { buildOp with 
          MSBuildParams = packArgs buildOp.MSBuildParams
    })


let currentTimeFileFormat () = DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss")

// --------------------------------------------------------------------------------------
// Clean build results

Target.create "Clean" (fun _ ->
    Shell.cleanDirs ["bin"; "temp"; "src" ++ "XrmContext" ++ "bin"]
)

// --------------------------------------------------------------------------------------
// Build library & test project
Target.create "Build" (fun _ ->
    !! solutionFile
    |> Seq.head
    |> commonBuild
)


// --------------------------------------------------------------------------------------
// Build a NuGet package
Target.create "NuGet" (fun _ ->
  let packArgs (defaults:MSBuild.CliArguments) = 
    { defaults with
        NoWarn = Some(["NU5100"])
        Properties = 
        [
          "Version", release.NugetVersion
          "ReleaseNotes", String.Join(Environment.NewLine, release.Notes)
        ] 
    }
  DotNet.pack (fun def -> 
    { def with
        NoBuild = true
        MSBuildParams = packArgs def.MSBuildParams
        OutputPath = Some("bin")
        
    }) projectPath)
              

Target.create "PublishNuGet" (fun _ -> 
  let setNugetPushParams (defaults:NuGetPushParams) =
    { defaults with
        ApiKey = Fake.Core.Environment.environVarOrDefault "delegateas-nugetkey" "" |> Some
        Source = Some "https://api.nuget.org/v3/index.json"
    }
  let setParams (defaults:DotNet.NuGetPushOptions) =
      { defaults with
          PushParams = setNugetPushParams defaults.PushParams
       }
  let nugetPacakge = !!("bin/Delegate."+project+".*.nupkg") |> Seq.head
  DotNet.nugetPush setParams nugetPacakge)


// --------------------------------------------------------------------------------------
// Generate the documentation
Target.create "CleanDocs" ignore
Target.create "GenerateReferenceDocs" ignore
Target.create "GenerateHelp" ignore
Target.create "GenerateHelpDebug" ignore
Target.create "KeepRunning" ignore
Target.create "GenerateDocs" ignore
Target.create "AddLangDocs" ignore
// --------------------------------------------------------------------------------------
// Release Scripts

Target.create "ReleaseDocs" ignore
Target.create "Release" ignore
Target.create "BuildPackage" ignore

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target.create "All" ignore

"Clean"
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "NuGet"
  ==> "BuildPackage"
  ==> "All"
  
"BuildPackage"
  ==> "PublishNuget"

Target.runOrDefault "Build"
