using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Billing.API.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sap.Data.Hana;

namespace Billing.API.Services.Invoice
{
    public class InvoiceService : IInvoiceService
    {
        private readonly ILogger<InvoiceService> _logger;
        private readonly IOptions<InvoiceProviderOptions> _options;

        public InvoiceService(ILogger<InvoiceService> logger, IOptions<InvoiceProviderOptions> options)
        {
            _logger = logger;
            _options = options;
        }

        public async Task<IEnumerable<InvoiceListItem>> GetInvoices(string clientId)
        {
            var schema = _options.Value.Schema;

            await using var conn = new HanaConnection(_options.Value.DbConnectionString);

            await conn.OpenAsync();

            var query = string.Empty;

            query += $" SELECT DISTINCT";
            query += $"     T0.\"CardCode\" ,";
            query += $"     T0.\"CardName\" ,";
            query += $"     T0.\"SendDate\" ,";
            query += $"     T0.\"SendTime\" ,";
            query += $"     cast(T0.\"DocEntry\" AS NVARCHAR) AS \"DocEntry\" ,";
            query += $"     T2.\"DocTotal\" ,";
            query += $"     T2.\"PaidToDate\" ,";
            query += $"     cast(T2.\"DocCur\" AS NVARCHAR)   AS \"DocCur\" ,";
            query += $"     cast(T1.\"trgtPath\" AS NVARCHAR) AS \"trgtPath\" ,";
            query += $"     cast(T1.\"FileName\" AS NVARCHAR) AS \"FileName\" ,";
            query += $"     cast(T1.\"FileExt\" AS NVARCHAR)  AS \"FileExt\" ";
            query += $" FROM";
            query += $"     {schema}.oeml T0 ";
            query += $"     INNER JOIN {schema}.ATC1 T1 ON T0.\"AtcEntry\" = T1.\"AbsEntry\" ";
            query += $"     INNER JOIN {schema}.OINV T2 ON T0.\"DocEntry\" = T2.\"DocNum\" ";
            query += $"     INNER JOIN ( SELECT";
            query += $"         T0.\"DocEntry\" ,";
            query += $"         min(T0.\"SendTime\") AS \"SendTime\" ";
            query += $"         FROM {schema}.oeml T0 ";
            query += $"         WHERE T0.\"ObjType\" = '13' ";
            query += $"         GROUP BY T0.\"DocEntry\" ) x ON x.\"DocEntry\" = T0.\"DocEntry\" AND x.\"SendTime\" = T0.\"SendTime\"";
            query += $" WHERE";
            query += $"     T0.\"ObjType\" = '13'";
            query += $" AND T0.\"CardCode\" LIKE '%{clientId}%'";

            var da = new HanaDataAdapter(query, conn);

            var dt = new DataTable("Invoices");

            da.Fill(dt);

            await conn.CloseAsync();

            var items = new List<InvoiceListItem>();

            if (dt.Rows.Count > 0)
            {
                items.AddRange(Enumerable.Select(dt.AsEnumerable(), dr => new InvoiceListItem(dr.Field<string>("DocEntry"), dr.Field<string>("CardCode"), dr.Field<DateTime>("SendDate"), dr.Field<string>("DocCur"), dr.Field<decimal>("DocTotal").ToDouble(), $"{dr.Field<string>("trgtPath")}\\{dr.Field<string>("FileName")}.{dr.Field<string>("FileExt")}")));
            }

            return items;
        }

        public async Task TestSapConnection()
        {
            await Task.Run(() => throw new NotImplementedException());
        }
    }
}
