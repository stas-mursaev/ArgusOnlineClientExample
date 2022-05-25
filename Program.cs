using System;
using System.Data;
using System.IO;
using System.Linq;
using System.ServiceModel;

using WcfArgusOnline;

// How to add a web service reference to your own project
//
// 1. Install "dotnet-svcutil" by running the command
// dotnet tool install --global dotnet-svcutil
//
// 2. Create the folder "Connected Services" in your project folder
//
// 3. Create a web service reference by running the following command in the folder "\Connected Services"
// dotnet-svcutil https://www.argusmedia.com/ArgusWSVSTO/ArgusOnline.asmx?wsdl --targetFramework net6.0 --serializer XmlSerializer --outputDir WcfArgusOnline --namespace "*,WcfArgusOnline"


const string URL = "https://www.argusmedia.com/ArgusWSVSTO/ArgusOnline.asmx";

var address = new EndpointAddress(new Uri(URL));
var binding = new BasicHttpsBinding { MaxReceivedMessageSize = int.MaxValue };
var client = new ArgusWSSoapClient(binding, address);


//
// Authentication example
//

Console.Write("User name: ");
var Username = Console.ReadLine();
Console.Write("Password: ");
var Password = Console.ReadLine();

if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
{
    Console.WriteLine("Entered an empty user name or password, please try again");
    return;
}

Console.WriteLine("Authenticating ...");

var authenticationResult = await client.AuthenticateAsync(Username, Password);

Console.WriteLine($"LoginResult = {authenticationResult.LoginResult}, AuthToken = {authenticationResult.AuthToken}");
Console.WriteLine();

if (authenticationResult.LoginResult != 0)
{
    Console.WriteLine("Invalid user name or password, please try again");
    return;
}


//
// Getting some meta tables example
//

Console.WriteLine("Loading tables ...");

var tablesXml = await client.GetTablesAsync(authenticationResult.AuthToken, new[] { "V_CODE", "V_QUOTE", "V_TIMESTAMP" } );
var tables = LoadDataSet(tablesXml);

WriteDataSetSample(tables);


//
// Getting customPriceReport example
//

// Empty end date for the table V_CODE means that the price has current data
// currentCodes and timestamps are the dictionaries that we will use later to show price rows in more detail

var currentCodes = tables.Tables["V_CODE"].Rows.Cast<DataRow>()
    .Where(row => row["ENDDATE"] == DBNull.Value)
    .ToDictionary(k => (decimal)k["CODE_ID"], v => (string)v["DESCRIPTION"]);

var timestamps = tables.Tables["V_TIMESTAMP"].Rows.Cast<DataRow>()
    .ToDictionary(k => (decimal)k["TIME_STAMP_ID"], v => (string)v["DESCRIPTION"]);

// First, let's see what prices with current data are available and then take a few of them

var quotes = tables.Tables["V_QUOTE"].Rows.Cast<DataRow>()
    .Where(row => currentCodes.ContainsKey((decimal)row["CODE_ID"]))
    .Take(10);

// Second, let's make a query

var itemCounter = 0;
var parameters = new PriceReportParams
{
    // Set StartDate = EndDate = DateTime.MinValue to query the latest values available
    StartDate = DateTime.MinValue,
    EndDate = DateTime.MinValue,
    Periodicity = EnPeriodicity.Daily,
    ItemList = quotes.Select(q => 
        // -1 down here means a wildcard
        new PriceReportItem()
        {
            PriceReportItemID = itemCounter++,
            QuoteId = (decimal)q["QUOTE_ID"],
            PriceTypeId = -1,
            TimestampId = -1,
            ForwardPeriod = -1,
            ForwardYear = -1,
            CurrencyUnit = -1,
            MeasureUnit = -1
        }).ToArray()
};

// And finally, run it

Console.WriteLine("Loading customPriceReport ...");

var reportXml = await client.GetCustomReportAsync(authenticationResult.AuthToken, parameters);
var report = LoadDataSet(reportXml);

// Let's see what the web service returned

WriteDataSetSample(report);

// Let's show the first line of the report in more detail

var firstRow = report.Tables["V_REPO"].Rows.Cast<DataRow>().FirstOrDefault();
if (firstRow != null)
{
    Console.WriteLine("First line of the report in more detail:");
    Console.WriteLine($"Code description = {currentCodes[(decimal)firstRow["CODE_ID"]]}");
    Console.WriteLine($"Timestamp description = {timestamps[(decimal)firstRow["TIMESTAMP_ID"]]}");
}



static DataSet LoadDataSet(ArrayOfXElement xml)
{
    var dataSet = new DataSet();
    string rawXml;

    // Reading schema ...
    rawXml = xml.Nodes[0].ToString();
    dataSet.ReadXmlSchema(new StringReader(rawXml));

    // Reading data rows
    rawXml = xml.Nodes[1].ToString();
    dataSet.ReadXml(new StringReader(rawXml));
    
    return dataSet;
}

static void WriteDataSetSample(DataSet dataSet)
{
    Console.WriteLine($"Got {dataSet.Tables.Count} tables");
    Console.WriteLine();

    foreach (DataTable table in dataSet.Tables)
    {
        Console.WriteLine($"Table {table.TableName}");

        Console.WriteLine($"  Row count = {table.Rows.Count}\n");

        Console.WriteLine($"  Columns:");
        foreach (DataColumn column in table.Columns)
            Console.WriteLine($"  {column.ColumnName}\t{column.DataType.Name}");
        Console.WriteLine();

        Console.WriteLine($"  Data sample:");
        foreach (var row in table.Rows.Cast<DataRow>().Take(10))
        {
            foreach (var item in row.ItemArray)
                Console.Write($"  {item}\t");
            Console.WriteLine();
        }
        Console.WriteLine();
    }
}