﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace StateStreetGang.AspNet.Identity.AzureTable
{
    // Implementation details for IUserRoleStore<T>
    public partial class AzureTableUserStore<T> :
        IUserRoleStore<T>
    {
        #region props

        /// <summary>
        /// Gets the name of the Azure table that stores the user role information.
        /// </summary>
        /// <value></value>
        protected virtual string UserRoleTableName
        {
            get { return DefaultUserRoleTableName; }
        }

        private readonly Func<T, string> _defaultMapUserToUserRolePartitionKey = x => AzureTableUserRoles.DefaultAzureTableUserRolesPartitionKey;

        /// <summary>
        /// Maps a A <see cref="AzureTableUser"/>-derived type to a partition key for user role data.
        /// </summary>
        protected virtual Func<T, string> MapUserToUserRolePartitionKey
        {
            get
            {
                return _defaultMapUserToUserRolePartitionKey;
            }
        }

        /// <summary>
        /// Roles are stored as a delimited list.  This property returns the delimiter used.
        /// </summary>
        /// <remarks>It is important that the delimiter not be found in any role string.</remarks>
        protected virtual string UserRoleDelimiter
        {
            get
            {
                return "|";
            }
        }
        #endregion

        #region IUserRoleStore<T> Members

        /// <summary>
        /// Adds the given <paramref name="role"/> to the given <paramref name="user"/>.
        /// </summary>
        /// <param name="user">A <see cref="AzureTableUser"/>-derived type</param>
        /// <param name="role">The role to add.</param>
        /// <returns><see cref="Task"/></returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="user"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="role"/> is empty or consists only of whitespace, or if the role contains the <see cref="UserRoleDelimiter"/> string.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="role"/> is <c>null</c>.</exception>
        /// <see cref="AzureTableUserException">Thrown whenever a table operation results in a <see cref="StorageException"/> being thrown.</see>
        public virtual async Task AddToRoleAsync(T user, string roleName)
        {
            AssertNotDisposed();
            if (user == null)
                throw new ArgumentNullException("user");
            ValidateRole(roleName);
            try
            {
                var roles = await GetRolesForUser(user);
                roles.Roles = Join(roles.Roles, roleName);
                var query = TableOperation.InsertOrReplace(roles);
                await Run(UserRoleTableName, query);
            }
            catch (StorageException ex)
            {
                throw new AzureTableUserException("An exception was thrown while attempting to update the roles for this user.  See the inner exception for details.", ex);
            }
        }
        /// <summary>
        /// Gets all roles for the given <paramref name="user"/>.
        /// </summary>
        /// <param name="user">A <see cref="AzureTableUser"/>-derived type</param>
        /// <returns>A <see cref="Task{T}"/> that returns an <see cref="IList{T}"/> of role names for the user.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="user"/> is <c>null</c>.</exception>
        /// <see cref="AzureTableUserException">Thrown whenever a table operation results in a <see cref="StorageException"/> being thrown.</see>
        public virtual async Task<IList<string>> GetRolesAsync(T user)
        {
            AssertNotDisposed();
            if (user == null)
                throw new ArgumentNullException("user");

            try
            {
                var roles = await GetRolesForUser(user);
                return Split(roles.Roles).ToList();
            }
            catch (StorageException ex)
            {
                throw new AzureTableUserException("An exception was thrown while attempting to get the roles for this user.  See the inner exception for details.", ex);
            }
        }
        /// <summary>
        /// Checks if the given <paramref name="user"/> is a member of the given <paramref name="role"/>.
        /// </summary>
        /// <param name="user">A <see cref="AzureTableUser"/>-derived type</param>
        /// <param name="role">The role to check for.</param>
        /// <returns><c>True</c> if the <paramref name="user"/> is a member of the given <paramref name="role"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="user"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="role"/> is <c>null</c> or empty.</exception>
        /// <see cref="AzureTableUserException">Thrown whenever a table operation results in a <see cref="StorageException"/> being thrown.</see>
        public virtual async Task<bool> IsInRoleAsync(T user, string roleName)
        {
            AssertNotDisposed();
            if (user == null)
                throw new ArgumentNullException("user");
            ValidateRole(roleName);

            try
            {
                var ur = await GetRolesForUser(user);
                var roles = Split(ur.Roles);
                return roles.Any(x => x.Equals(roleName, StringComparisonType));
            }
            catch (StorageException ex)
            {
                throw new AzureTableUserException("An exception was thrown while attempting to get the roles for this user.  See the inner exception for details.", ex);
            }
        }
        /// <summary>
        /// Removes the given <paramref name="user"/> is in the given <paramref name="role"/>.
        /// </summary>
        /// <param name="user">A <see cref="AzureTableUser"/>-derived type</param>
        /// <param name="role">The role to check</param>
        /// <returns><c>True</c> if the given <paramref name="user"/> is in the given <paramref name="role"/>, and <c>false</c> otherwise.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="user"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="role"/> is <c>null</c> or empty.</exception>
        /// <see cref="AzureTableUserException">Thrown whenever a table operation results in a <see cref="StorageException"/> being thrown.</see>
        public virtual async Task RemoveFromRoleAsync(T user, string roleName)
        {
            AssertNotDisposed();
            if (user == null)
                throw new ArgumentNullException("user");
            ValidateRole(roleName);
            try
            {
                var roles = await GetRolesForUser(user);
                roles.Roles = RemoveRoles(roles.Roles, roleName);
                await Run(UserRoleTableName, TableOperation.InsertOrReplace(roles));
            }
            catch (StorageException ex)
            {
                throw new AzureTableUserException("An exception was thrown while attempting to get the roles for this user.  See the inner exception for details.", ex);
            }
        }

        #endregion

        #region privates
        /// <summary>
        /// Gets the user role partition key without the risk of NREs
        /// </summary>
        /// <param name="user">A <see cref="AzureTableUser"/>-derived type</param>
        /// <returns>The user role partition key</returns>
        private string SafeGetUserRolePartitionKey(T user)
        {
            return (MapUserToUserRolePartitionKey ?? _defaultMapUserToUserRolePartitionKey)(user);
        }
        /// <summary>
        /// Validates the given <paramref name="role"/>, ensuring it isn't null, empty/whitespace only, and doesn't contain the <see cref="UserRoleDelimiter"/>.
        /// </summary>
        /// <param name="role"></param>
        private void ValidateRole(string role)
        {
            if (role == null)
                throw new ArgumentNullException("role");
            if (string.IsNullOrWhiteSpace(role))
                throw new ArgumentException("role cannot be null, empty, or consist of whitespace.");
            if (role.Contains(UserRoleDelimiter))
                throw new ArgumentException("The role contains an illegal character: " + UserRoleDelimiter, "role");
        }

        /// <summary>
        /// Gets the <see cref="AzureTableUserRoles"/> for the given <paramref name="user"/>.
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        /// <remarks>Performs null check on <paramref name="user"/>, and creates a new <see cref="AzureTableUserRoles"/> instance if one is not found for the user.</remarks>
        private async Task<AzureTableUserRoles> GetRolesForUser(T user)
        {
            if (user == null)
                throw new ArgumentNullException("user");
            var partitionKey = SafeGetUserRolePartitionKey(user);
            var table = await GetTable(UserRoleTableName);
            var query = TableOperation.Retrieve<AzureTableUserRoles>(partitionKey, user.RowKey);
            AzureTableUserRoles retval = null;
            try
            {
                var result = await table.ExecuteAsync(query);
                retval = result.Result as AzureTableUserRoles;                
            }catch(StorageException ex)
            {
                if (ex.RequestInformation.HttpStatusCode != (int)HttpStatusCode.NotFound)
                    throw new AzureTableUserException("An exception was thrown while attempting to get the roles for this user.  See the inner exception for details.", ex);
            }
            retval = retval ?? new AzureTableUserRoles
                {
                    PartitionKey = (MapUserToUserRolePartitionKey ?? _defaultMapUserToUserRolePartitionKey)(user),
                    Roles = "",
                    UserId = user.Id,
                };
            retval.EnsureETagSet();
            return retval;
        }
        /// <summary>
        /// Joins the given <paramref name="roles"/> with <paramref name="newRoles"/>.
        /// </summary>
        /// <param name="roles">The current role string</param>
        /// <param name="newRoles">One or more roles</param>
        /// <returns>The joined role string.</returns>
        private string Join(string roles, params string[] newRoles)
        {
            return Join(Split(roles ?? "").Union(newRoles));
        }
        /// <summary>
        /// joins all the given <paramref name="roles"/> into a delimited role string.
        /// </summary>
        /// <param name="roles">All the roles to join</param>
        /// <returns>The delimited role string</returns>
        private string Join(IEnumerable<string> roles)
        {
            return string.Join(UserRoleDelimiter, roles.Select(x => x.Trim()).Distinct(StringComparerType).OrderBy(x => x, StringComparerType));
        }
        /// <summary>
        /// Removes one or more roles from the given <paramref name="roles"/> string.
        /// </summary>
        /// <param name="roles">The current delimited roles string.</param>
        /// <param name="toRemove">The roles to remove</param>
        /// <returns></returns>
        private string RemoveRoles(string roles, params string[] toRemove)
        {
            var split = Split(roles);
            return Join(split.Except(toRemove, StringComparerType));
        }

        /// <summary>
        /// Splits the <paramref name="chunk"/> of text on the configured <see cref="UserRoleDelimiter"/>
        /// </summary>
        /// <param name="chunk">The chunk of text to split</param>
        /// <returns>An <see cref="IEnumerable{T}"/> containing each chunk.</returns>
        private IEnumerable<string> Split(string chunk)
        {
            if (string.IsNullOrWhiteSpace(chunk))
                yield break;
            foreach (var item in chunk.Split(new string[] { UserRoleDelimiter }, StringSplitOptions.RemoveEmptyEntries))
                yield return item.Trim();
        }
        #endregion
    }
}
