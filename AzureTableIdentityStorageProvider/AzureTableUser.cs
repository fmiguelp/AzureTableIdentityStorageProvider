﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;

namespace StateStreetGang.AspNet.Identity.AzureTable
{
    /// <summary>
    /// An implementation of <see cref="IUser"/> that can be stored in an Azure Table.
    /// </summary>
    public class AzureTableUser : Microsoft.WindowsAzure.Storage.Table.TableEntity, IUser
    {
        /// <summary>
        /// The default partition key used for Asp.Net User instances.
        /// </summary>
        public const string DefaultAspNetUserPartitionKey = "AspNetUserPartition";

        private string _userName;

        /// <summary>
        /// Initializes a new instance of the <see cref="AzureTableUser" /> class.
        /// </summary>
        public AzureTableUser()
        {
            PartitionKey = DefaultAspNetUserPartitionKey;
        }

        #region IUser Members

        /// <summary>
        /// Unique key for the user
        /// </summary>
        /// <remarks>the Id is also used as the <see cref="Microsoft.WindowsAzure.Storage.Table.TableEntity.RowKey"/>.</remarks>
        /// <exception cref="ArgumentException">Thrown if <paramref name="value"/> is <c>null</c> or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
        public virtual string Id
        {
            get { return this.RowKey; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value");
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("value cannot be null, empty, or consist of whitespace.");
                this.RowKey = value;
            }
        }

        /// <summary>
        /// Gets or sets the name of the user.
        /// </summary>
        /// <value>The name of the user.</value>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="value"/> is empty.</exception>
        public virtual string UserName
        {
            get { return this._userName; }
            set
            {
                if (value == null)
                    throw new ArgumentNullException("value");
                if (string.IsNullOrWhiteSpace(value))
                    throw new ArgumentException("value cannot be empty or consist of whitespace.");
                this._userName = value;
                SearchUserName = value.ToUpperInvariant();
            }
        }

        /// <summary>
        /// A hash of the user's password.
        /// </summary>
        public virtual string PasswordHash { get; set; }

        /// <summary>
        /// This is the user name used for searching by user name.  Do not set this from within code.
        /// </summary>
        /// <remarks>Azure Table searches are <i>case sensitive</i>. Identity depends on user name searches to prevent multiple users with the same name (it doesn't support separation of identity from user name, unfortunately). Therefore we must consider <see cref="UserName"/> the name for display, and this property the name for searching. This property is set by <see cref="UserName"/>, and should not be set by user code.</remarks>
        public virtual string SearchUserName { get; set; }

        #endregion
    }
}
