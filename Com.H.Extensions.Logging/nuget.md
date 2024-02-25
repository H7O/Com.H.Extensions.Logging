# Com.H.Extensions.Logging
Adds Database ILogger provider that makes it easy to log to a Database instead of a file or console.
It allows users to customize their logging DB table, columns and SQL commands to suit their needs.

## How to use

This library is designed to accept DbConnection objects, which means it can work with any ADO.NET provider that implements DbConnection classes.
It looks for specific configuration in your settings file (e.g. appsettings.json or appsettings.xml), which is used to configure the database logger provider.

For source code and documentation, kindly visit the project's github page [https://github.com/H7O/Com.H.Extensions.Logging](https://github.com/H7O/Com.H.Extensions.Logging)

Below is a sample of how to use this library in a .NET Core Worker Service.

## Example usage in a .NET Core Worker Service
This sample demonstrates how to configure appsettings.xml (note the .xml extension) instead of appsettings.json which makes it easier to work with special characters without escaping them.

> Although you can use appsettings.json, it is recommended to use appsettings.xml for better readability and to avoid escaping special characters.
> Also, you can use both appsettings.json and appsettings.xml at the same time, where you'd have your other app settings in appsettings.json (if you're more comfortable using that for your settings) and your logging settings in appsettings.xml.

1) First, let's create a worker service to use as a sample to showcase how to use this library using the .NET CLI
```bash
dotnet new worker -n DbLoggerTest
```
> **Note:** The -n parameter is used to specify the name of the project. You can use any name you want.

2) Next let's add NuGet packages Com.H.Extensions.Logging, Microsoft.Data.SqlClient (which is the ADO.NET provider for SQL Server) and Microsoft.Extensions.Configuration.Xml (which is used to read the appsettings.xml file) to our project.
```bash
dotnet add package Com.H.Extensions.Logging
dotnet add package Microsoft.Data.SqlClient
dotnet add package Microsoft.Extensions.Configuration.Xml
```
> **Note**: The above commands will add Com.H.Extensions.Logging package, Microsoft.Data.SqlClient and Microsoft.Extensions.Configuration.Xml to our project. 
You can do that manually by adding the package to your .csproj file or using the NuGet package manager GUI in Visual Studio.

3) Now let's create the database table that will be used for logging. Run the following SQL script in your SQL Server database.
```sql
-- create a database called logging_test_db
create database logging_test_db;
```

4) Next let's setup our appsettings.xml file. Create a new file called appsettings.xml in the root of your project and add the following content to it.
```xml
<settings>
  <ConnectionStrings>
    <log_db><![CDATA[Data Source=localhost\sql2022;Initial Catalog=logging_test_db;Integrated Security=True;TrustServerCertificate=True;]]></log_db>
  </ConnectionStrings>

  <db_logging>
    <log_level>
      <default>Information</default>
      <category>
        <name>Microsoft.Hosting.Lifetime</name>
        <level>Information</level>
      </category>
      <category>
        <name>DbLoggerTest.Worker</name>
        <level>Information</level>
      </category>
    </log_level>

    <init_query>
      <![CDATA[
        use [logging_test_db]
        -- check if general_log table exists and create it if not
        if not exists (select * from sysobjects where name='general_log' and xtype='U')
        begin
          SET ANSI_NULLS ON
          SET QUOTED_IDENTIFIER ON
          
          CREATE TABLE [dbo].[general_log](
            [id] [uniqueidentifier] NOT NULL,
            [sort_id] [bigint] IDENTITY(1,1) NOT NULL,
            [c_date] [datetime] NULL,
            [level] [nvarchar](50) NULL,
            [category_name] [nvarchar](100) NULL,
            [message] [nvarchar](4000) NULL,
            [exception] [nvarchar](4000) NULL,
            [event_id] [int] NULL
            CONSTRAINT [PK_general_logs] PRIMARY KEY CLUSTERED 
            (
              [id] ASC
            )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
          ) ON [PRIMARY]
          ALTER TABLE [dbo].[general_log] ADD  CONSTRAINT [DEFAULT_general_logs_id]  DEFAULT (newid()) FOR [id]
          ALTER TABLE [dbo].[general_log] ADD  CONSTRAINT [DEFAULT_general_logs_c_date]  DEFAULT (getdate()) FOR [c_date]
        end

        
      ]]>
    </init_query>

    <log_query>
      <![CDATA[
        declare @message nvarchar(4000) = {{log_message}};
        declare @level nvarchar(50) = {{log_level}};
        declare @event_id int = {{log_event_id}};
        declare @category_name nvarchar(100) = {{log_category_name}};
        declare @exception nvarchar(4000) = {{log_exception}};
        
        insert into general_log
          (id, c_date, [message], [level], [event_id], [category_name],[exception])
          values 
          (newid(), getdate(), @message, @level, @event_id, @category_name, @exception);
      ]]>
    </log_query>

  </db_logging>
</settings>
```
Let's breakdown the above configuration file.
**ConnectionStrings > log_db**: This is the connection string to the database that will be used for logging.
**db_logging > log_level**: This is where you specify the log levels for different categories. 
In the above example, the default log level is `Information` for all categories. 
You can override the default log level for a specific category by specifying the category name and the log level you want to use.

In our example, we have two categories; the first is `Microsoft.Hosting.Lifetime` and the second is `DbLoggerTest.Worker`.

The first category is used by the host lifetime events and the second category is used by our worker service.

**db_logging > init_query**: This is the SQL query that will be executed when the logger is initialized. In our example, we are initializing the logging database by creating a table called `general_log` if it doesn't exist.
This is also where you can add any other initialization queries you want to run when the logger is initialized.

> **Note**: The `<![CDATA[]]>` tag is used to escape special characters in XML. It is recommended to use appsettings.xml instead of appsettings.json to avoid escaping special characters.

**db_logging > log_query**: This is the SQL query that will be executed when a log message is written. In our example, we are inserting the log message into the `general_log` table.
The reserved variables that get replaced with actual values are `{{log_message}}`, `{{log_level}}`, `{{log_event_id}}`, `{{log_category_name}}` and `{{log_exception}}`.

The `{{log_message}}` is replaced with the actual log message, `{{log_level}}` is replaced with the log level (e.g., Information, Error, Warning, etc.), `{{log_event_id}}` is replaced with the event id, 
`{{log_category_name}}` is replaced with the category name (in our example , it will be `Microsoft.Hosting.Lifetime` or `DbLoggerTest.Worker`) 
and `{{log_exception}}` is replaced with the exception message if there's an exception.

Any extra variables passed as part of the log message will also be replaced in the log_query.
For example, if you have a log message like this:
```csharp
_logger.LogInformation("This is a test message with extra data {name}", userName);
```
The `{{name}}` will be optionally available for use in the log_query, 
so if you have a log_query that makes use of `{{name}}`, it will be replaced with the actual value of `userName`.

You can also pass, DateTime, int, decimal, etc.. values as part of the log message and they will be replaced in the log_query.

> **Note**: The replacement is safe as the library uses parameterized queries to avoid SQL injection attacks.

5) Now let's modify the Program.cs file to use the database logger provider.

```csharp
using Com.H.Extensions.Logging;
namespace DbLoggerTest
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Configuration
                .AddXmlFile("appsettings.xml", optional: false, reloadOnChange: true);
            builder.Logging.AddProvider(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var logSection = config.GetSection("db_logging");
                var logConnectionString = config.GetConnectionString("log_db");
                var dbc = new Microsoft.Data.SqlClient.SqlConnection(logConnectionString);

                return new DbLoggerProvider(
                        logSection,
                        dbc
                        );
            });

               builder.Services.AddHostedService<Worker>();
            var host = builder.Build();
            host.Run();
        }
    }
}
```

6) Now let's modify the Worker.cs file to use the database logger provider.

```csharp
namespace DbLoggerTest
{
    public class Worker(ILogger<Worker> logger) : BackgroundService
    {
        private readonly ILogger<Worker> _logger = logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(1, "Worker running at: {time}", DateTimeOffset.Now, "info test message");
                _logger.LogWarning(2, "warning at: {time}", DateTimeOffset.Now, "warning test message");
                _logger.LogError(3, "error at: {time}", DateTimeOffset.Now);
                // you can also pass variables to the log message like so:
                // e.g.
                // _logger.LogError(3, "error at: {time} {extra_info1}, {extra_info2}", DateTimeOffset.Now, "some extra info", "another extra info");
                // the {extra_info1} and {extra_info2} will be replaced with the values of the variables passed in the log method
                // which in turn will be passed to your SQL script under appsettings.xml > db_logging > log_query
                // and can be used just as you would use any other SQL variable like {{time}} or {{level}} etc.
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
    
}
```

## Extra helpful settings that can be added to the appsettings.xml file

There are few extra settings that can be added to the appsettings.xml file to customize the behavior of the database logger provider.

```xml
<settings>
  <db_logging>
    <disabled>false</disabled>
    <cache_settings>false</cache_settings>
    <log_to_console>false</log_to_console>
  </db_logging>
```

**db_logging > disabled**: This setting is used to disable the database logger provider. If set to true, the database logger provider will be disabled and no logs will be written to the database. The default value is false.
**db_logging > cache_settings**: This setting is used to cache the settings from the appsettings.xml file. If set to true, the settings will be cached and will not be reloaded when the appsettings.xml file changes. The default value is false.
**db_logging > log_to_console**: This setting is used to log to the console in addition to the database. If set to true, the logs will be written to the console as well as the database. The default value is false.