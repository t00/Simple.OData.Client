﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Simple.OData.Client.Extensions;

namespace Simple.OData.Client
{
    public class ColumnCollection : Collection<Column>
    {
        internal ColumnCollection()
        {
        }

        internal ColumnCollection(IEnumerable<Column> columns)
            : base(columns.ToList())
        {
        }

        public Column Find(string columnName)
        {
            var column = TryFind(columnName);

            if (column == null) 
                throw new UnresolvableObjectException(columnName, string.Format("Column {0} not found", columnName));

            return column;
        }

        public bool Contains(string columnName)
        {
            return TryFind(columnName) != null;
        }

        private Column TryFind(string columnName)
        {
            columnName = columnName.Homogenize();
            return this.SingleOrDefault(c => c.HomogenizedName.Equals(columnName));
        }
    }
}
