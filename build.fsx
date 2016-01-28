﻿#I "Src/packages/FAKE.3.30.1/tools"
#r "FakeLib.dll"

open Fake
open Fake.AssemblyInfoFile
open Fake.EnvironmentHelper
open Fake.MSBuildHelper
open Fake.NuGetHelper
open Fake.Testing

// properties
let semanticVersion = "3.0.2"
let version = (>=>) @"(?<major>\d*)\.(?<minor>\d*)\.(?<build>\d*).*?" "${major}.${minor}.${build}.0" semanticVersion
let configuration = getBuildParamOrDefault "configuration" "Release"
let deployToNuGet = getBuildParamOrDefault "deployToNuGet" "false"
let genDir = "Gen/"
let docDir = "Doc/"
let srcDir = "Src/"
let testDir = genDir @@ "Test"
let nugetDir = genDir @@ "NuGet"
let tempDir = genDir @@ "Temp"

RestorePackages()

Target "Clean" (fun _ ->
    CleanDirs[genDir; testDir; nugetDir; tempDir]

    build (fun p ->
        { p with
            Verbosity = Some(Quiet)
            Targets = ["Clean"]
            Properties = ["Configuration", configuration]
        })
        (srcDir @@ "KBCsv.sln")
)

Target "Build" (fun _ ->
    // generate the shared assembly info
    CreateCSharpAssemblyInfoWithConfig (srcDir @@ "AssemblyInfoCommon.cs")
        [
            Attribute.Version version
            Attribute.FileVersion version
            Attribute.Configuration configuration
            Attribute.Company "Kent Boogaart"
            Attribute.Product "KBCsv"
            Attribute.Copyright "© Copyright. Kent Boogaart."
            Attribute.Trademark ""
            Attribute.Culture ""
            Attribute.CLSCompliant true
            Attribute.StringAttribute("NeutralResourcesLanguage", "en-US", "System.Resources")
            Attribute.StringAttribute("AssemblyInformationalVersion", semanticVersion, "System.Reflection")
        ]
        (AssemblyInfoFileConfig(false))

    build (fun p ->
        { p with
            Verbosity = Some(Quiet)
            Targets = ["Build"]
            Properties =
                [
                    "Optimize", "True"
                    "DebugSymbols", "True"
                    "Configuration", configuration
                ]
        })
        (srcDir @@ "KBCsv.sln")
)

Target "ExecuteUnitTests" (fun _ ->
    xUnit2 (fun p ->
        { p with
            ShadowCopy = false;
//            HtmlOutputPath = Some testDir;
//            XmlOutputPath = Some testDir;
        })
        [
            srcDir @@ "KBCsv.UnitTests/bin" @@ configuration @@ "KBCsv.UnitTests.dll"
            srcDir @@ "KBCsv.Extensions.UnitTests/bin" @@ configuration @@ "KBCsv.Extensions.UnitTests.dll"
            srcDir @@ "KBCsv.Extensions.Data.UnitTests/bin" @@ configuration @@ "KBCsv.Extensions.Data.UnitTests.dll"
        ]
)

Target "ExecutePerformanceTests" (fun _ ->
    xUnit2 (fun p ->
        { p with
            ShadowCopy = false;
//            HtmlOutputPath = Some testDir;
//            XmlOutputPath = Some testDir;
        })
        [
            srcDir @@ "KBCsv.PerformanceTests/bin" @@ configuration @@ "KBCsv.PerformanceTests.exe"
        ]
)

Target "CreateArchives" (fun _ ->
    // source archive
    !! "**/*.*"
        -- ".git/**"
        -- (genDir @@ "**")
        -- (srcDir @@ "packages/**/*")
        -- (srcDir @@ "**/*.suo")
        -- (srcDir @@ "**/*.csproj.user")
        -- (srcDir @@ "**/*.gpState")
        -- (srcDir @@ "**/bin/**")
        -- (srcDir @@ "**/obj/**")
        |> Zip "." (genDir @@ "KBCsv-" + semanticVersion + "-src.zip")

    // binary archive
    !! (srcDir @@ "KBCsv/bin" @@ configuration @@ "KBCsv.*")
        ++ (srcDir @@ "KBCsv.Extensions/bin" @@ configuration @@ "KBCsv.Extensions.*")
        ++ (srcDir @@ "KBCsv.Extensions.Data/bin" @@ configuration @@ "KBCsv.Extensions.Data.*")
        |> CopyFiles tempDir

    !! (tempDir @@ "**")
        |> Zip tempDir (genDir @@ "KBCsv-" + semanticVersion + "-bin.zip")
)

Target "CreateNuGetPackages" (fun _ ->
    // copy binaries
    !! (srcDir @@ "KBCsv/bin" @@ configuration @@ "KBCsv.*")
        |> CopyFiles (nugetDir @@ "KBCsv/lib/portable-win+net45+wp8+MonoAndroid10+Xamarin.iOS10+MonoTouch10")

    !! (srcDir @@ "KBCsv.Extensions/bin" @@ configuration @@ "KBCsv.Extensions.*")
        |> CopyFiles (nugetDir @@ "KBCsv.Extensions/lib/portable-win+net45+wp8+MonoAndroid10+Xamarin.iOS10+MonoTouch10")

    !! (srcDir @@ "KBCsv.Extensions.Data/bin" @@ configuration @@ "KBCsv.Extensions.Data.*")
        |> CopyFiles (nugetDir @@ "KBCsv.Extensions.Data/lib/net45")

    // copy source
    let sourceFiles = [!! (srcDir @@ "**/*.*")
                    -- (srcDir @@ "packages/**/*")
                    -- (srcDir @@ "**/*.suo")
                    -- (srcDir @@ "**/*.csproj.user")
                    -- (srcDir @@ "**/*.gpState")
                    -- (srcDir @@ "**/bin/**")
                    -- (srcDir @@ "**/obj/**")]

    sourceFiles
        |> CopyWithSubfoldersTo (nugetDir @@ "KBCsv")

    sourceFiles
        |> CopyWithSubfoldersTo (nugetDir @@ "KBCsv.Extensions")

    sourceFiles
        |> CopyWithSubfoldersTo (nugetDir @@ "KBCsv.Extensions.Data")

    // create the NuGets
    NuGet (fun p ->
        {p with
            Project = "KBCsv"
            Version = semanticVersion
            OutputPath = nugetDir
            WorkingDir = nugetDir @@ "KBCsv"
            SymbolPackage = NugetSymbolPackage.Nuspec
            Publish = System.Convert.ToBoolean(deployToNuGet)
        })
        (srcDir @@ "KBCsv.nuspec")

    NuGet (fun p ->
        {p with
            Project = "KBCsv.Extensions"
            Version = semanticVersion
            OutputPath = nugetDir
            WorkingDir = nugetDir @@ "KBCsv.Extensions"
            SymbolPackage = NugetSymbolPackage.Nuspec
            Publish = System.Convert.ToBoolean(deployToNuGet)
        })
        (srcDir @@ "KBCsv.Extensions.nuspec")

    NuGet (fun p ->
        {p with
            Project = "KBCsv.Extensions.Data"
            Version = semanticVersion
            OutputPath = nugetDir
            WorkingDir = nugetDir @@ "KBCsv.Extensions.Data"
            SymbolPackage = NugetSymbolPackage.Nuspec
            Publish = System.Convert.ToBoolean(deployToNuGet)
        })
        (srcDir @@ "KBCsv.Extensions.Data.nuspec")
)

// build order
"Clean"
    ==> "Build"
    ==> "ExecuteUnitTests"
    ==> "CreateArchives"
    ==> "CreateNuGetPackages"

"Clean"
    ==> "Build"
    ==> "ExecutePerformanceTests"

RunTargetOrDefault "CreateNuGetPackages"