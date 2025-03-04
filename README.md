Application folder:
```bash
FIleScannerTool/FIleScannerTool/
```

Configuration file:
```bash
FIleScannerTool/FIleScannerTool/appsettings.json
```
Set *AWS.Region* and *AWS.BucketName* and\or *OutputCsvFilePath*

# Running .NET Console App on Linux

This guide outlines the steps to run a .NET console application on a Linux environment.

## Prerequisites

* **.NET Runtime:** Ensure the .NET runtime is installed on your Linux system.

    * For installation instructions, refer to the official Microsoft documentation: [Install .NET on Linux](https://docs.microsoft.com/en-us/dotnet/core/install/linux)

## Steps

1.  **Publish the Application:**

    * Navigate to your project directory in the terminal.
    * Execute the following command to publish your application:

        ```bash
        dotnet publish -c Release -r linux-x64 --self-contained true
        ```

        * `-c Release`: Publishes the application in release mode.
        * `-r linux-x64`: Specifies the target runtime as Linux 64-bit. Adjust this if necessary for your environment.
        * `--self-contained true`: Creates a self-contained deployment, including the .NET runtime. if you would rather have a framework dependant deployment, remove this.

    * The published application will be located in the `bin/Release/net[version]/linux-x64/publish/` directory.

2.  **Make the Application Executable:**

    * Navigate to the `publish` directory in the terminal.
    * Grant execute permissions to your application:

        ```bash
        chmod +x YourAppName
        ```

        * Replace `YourAppName` with the actual name of your executable.

3.  **Run the Application:**

    * Execute the application:

        ```bash
        ./YourAppName
        ```

## Notes

* For framework-dependent deployments, omit the `--self-contained true` flag during the `dotnet publish` step.
* Adjust the `-r` runtime identifier to match your Linux distribution and architecture.
* If you encounter any issues, consult the official .NET documentation or search for distribution-specific troubleshooting guides.
