﻿// Project: aguacongas/Identity.Firebase
// Copyright (c) 2020 @Olivier Lefebvre
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace Aguacongas.Identity.Firestore
{
    /// <summary>
    /// Represents a new instance of a persistence store for <see cref="IdentityUser"/>.
    /// </summary>
    public class UserOnlyStore : UserOnlyStore<IdentityUser<string>>
    {
        /// <summary>
        /// Constructs a new instance of <see cref="UserStore{TUser, TRole, TKey}"/>.
        /// </summary>
        /// <param name="db">The <see cref="FirestoreDb"/>.</param>
        /// <param name="tableNamesConfig"><see cref="FirestoreTableNamesConfig"/></param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/>.</param>
        public UserOnlyStore(FirestoreDb db, FirestoreTableNamesConfig tableNamesConfig, IdentityErrorDescriber describer = null) : base(db, tableNamesConfig, describer) { }
    }

    /// <summary>
    /// Represents a new instance of a persistence store for the specified user and role types.
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    public class UserOnlyStore<TUser> : UserOnlyStore<TUser, IdentityUserClaim<string>, IdentityUserLogin<string>, IdentityUserToken<string>>
        where TUser : IdentityUser<string>, new()
    {
        /// <summary>
        /// Constructs a new instance of <see cref="UserStore{TUser, TRole, TKey}"/>.
        /// </summary>
        /// <param name="db">The <see cref="FirestoreDb"/>.</param>
        /// <param name="tableNamesConfig"><see cref="FirestoreTableNamesConfig"/></param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/>.</param>
        public UserOnlyStore(FirestoreDb db, FirestoreTableNamesConfig tableNamesConfig, IdentityErrorDescriber describer = null) : base(db, tableNamesConfig, describer) { }
    }


    /// <summary>
    /// Represents a new instance of a persistence store for the specified user and role types.
    /// </summary>
    /// <typeparam name="TUser">The type representing a user.</typeparam>
    /// <typeparam name="TUserClaim">The type representing a claim.</typeparam>
    /// <typeparam name="TUserLogin">The type representing a user external login.</typeparam>
    /// <typeparam name="TUserToken">The type representing a user token.</typeparam>
    [SuppressMessage("Major Code Smell", "S2436:Types and methods should not have too many generic parameters", Justification = "Follow EF implementation")]
    public class UserOnlyStore<TUser, TUserClaim, TUserLogin, TUserToken> :
        FirestoreUserStoreBase<TUser, TUserClaim, TUserLogin, TUserToken>,
        IUserLoginStore<TUser>,
        IUserClaimStore<TUser>,
        IUserPasswordStore<TUser>,
        IUserSecurityStampStore<TUser>,
        IUserEmailStore<TUser>,
        IUserLockoutStore<TUser>,
        IUserPhoneNumberStore<TUser>,
        IUserTwoFactorStore<TUser>,
        IUserAuthenticationTokenStore<TUser>,
        IUserAuthenticatorKeyStore<TUser>,
        IUserTwoFactorRecoveryCodeStore<TUser>
        where TUser : IdentityUser<string>, new()
        where TUserClaim : IdentityUserClaim<string>, new()
        where TUserLogin : IdentityUserLogin<string>, new()
        where TUserToken : IdentityUserToken<string>, new()
    {

        private readonly FirestoreDb _db;
        private readonly CollectionReference _users;
        private readonly CollectionReference _usersLogins;
        private readonly CollectionReference _usersClaims;
        private readonly CollectionReference _usersTokens;

        /// <summary>
        /// A navigation property for the users the store contains.
        /// </summary>
        public override IQueryable<TUser> Users
        {
            get
            {
                var documents = _users.GetSnapshotAsync().GetAwaiter().GetResult();
                return documents.Select(d => Map.FromDictionary<TUser>(d.ToDictionary())).AsQueryable();
            }
        }

        /// <summary>
        /// Creates a new instance of the store.
        /// </summary>
        /// <param name="db">The <see cref="FirestoreDb"/>.</param>
        /// <param name="tableNamesConfig"><see cref="FirestoreTableNamesConfig"/></param>
        /// <param name="describer">The <see cref="IdentityErrorDescriber"/> used to describe store errors.</param>
        public UserOnlyStore(FirestoreDb db, FirestoreTableNamesConfig tableNamesConfig, IdentityErrorDescriber describer = null) : base(describer ?? new IdentityErrorDescriber())
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            tableNamesConfig = tableNamesConfig ?? throw new ArgumentNullException(nameof(tableNamesConfig));
            _users = db.Collection(tableNamesConfig.UsersTableName);
            _usersLogins = db.Collection(tableNamesConfig.UserLoginsTableName);
            _usersClaims = db.Collection(tableNamesConfig.UserClaimsTableName);
            _usersTokens = db.Collection(tableNamesConfig.UserTokensTableName);
        }

        /// <summary>
        /// Creates the specified <paramref name="user"/> in the user store.
        /// </summary>
        /// <param name="user">The user to create.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/> of the creation operation.</returns>
        public async override Task<IdentityResult> CreateAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            AssertNotNull(user, nameof(user));

            var dictionary = Map.ToDictionary(user);
            await _users.Document(user.Id).SetAsync(dictionary, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return IdentityResult.Success;
        }

        /// <summary>
        /// Updates the specified <paramref name="user"/> in the user store.
        /// </summary>
        /// <param name="user">The user to update.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/> of the update operation.</returns>
        public override Task<IdentityResult> UpdateAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            AssertNotNull(user, nameof(user));

            var dictionary = Map.ToDictionary(user);
            return _db.RunTransactionAsync(async transaction =>
               {
                   var userRef = _users.Document(user.Id);
                   var snapShot = await transaction.GetSnapshotAsync(userRef, cancellationToken)
                    .ConfigureAwait(false);
                   if (snapShot.GetValue<string>("ConcurrencyStamp") != user.ConcurrencyStamp)
                   {
                       return IdentityResult.Failed(ErrorDescriber.ConcurrencyFailure());
                   }
                   transaction.Update(userRef, dictionary);
                   return IdentityResult.Success;
               }, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Deletes the specified <paramref name="user"/> from the user store.
        /// </summary>
        /// <param name="user">The user to delete.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation, containing the <see cref="IdentityResult"/> of the update operation.</returns>
        public override Task<IdentityResult> DeleteAsync(TUser user, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfDisposed();
            AssertNotNull(user, nameof(user));

            return _db.RunTransactionAsync(async transaction =>
            {
                var userRef = _users.Document(user.Id);
                var snapShot = await transaction.GetSnapshotAsync(userRef, cancellationToken)
                    .ConfigureAwait(false);
                if (snapShot.GetValue<string>("ConcurrencyStamp") != user.ConcurrencyStamp)
                {
                    return IdentityResult.Failed(ErrorDescriber.ConcurrencyFailure());
                }
                transaction.Delete(userRef);
                return IdentityResult.Success;
            }, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Finds and returns a user, if any, who has the specified <paramref name="userId"/>.
        /// </summary>
        /// <param name="userId">The user ID to search for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the user matching the specified <paramref name="userId"/> if it exists.
        /// </returns>
        public override async Task<TUser> FindByIdAsync(string userId, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            AssertNotNull(userId, nameof(userId));

            var snapShot = await _users.Document(userId).GetSnapshotAsync(cancellationToken)
                .ConfigureAwait(false);
            if (snapShot != null)
            {
                return Map.FromDictionary<TUser>(snapShot.ToDictionary());
            }

            return null;
        }

        /// <summary>
        /// Finds and returns a user, if any, who has the specified normalized user name.
        /// </summary>
        /// <param name="normalizedUserName">The normalized user name to search for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> that represents the asynchronous operation, containing the user matching the specified <paramref name="normalizedUserName"/> if it exists.
        /// </returns>
        public override async Task<TUser> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            AssertNotNullOrEmpty(normalizedUserName, nameof(normalizedUserName));

            var snapShot = await _users.WhereEqualTo("NormalizedUserName", normalizedUserName)
                .GetSnapshotAsync(cancellationToken)
                .ConfigureAwait(false);
            var document = snapShot.Documents
                .FirstOrDefault();
            if (document != null)
            {
                return Map.FromDictionary<TUser>(document.ToDictionary());
            }
            return null;
        }

        /// <summary>
        /// Get the claims associated with the specified <paramref name="user"/> as an asynchronous operation.
        /// </summary>
        /// <param name="user">The user whose claims should be retrieved.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>A <see cref="Task{TResult}"/> that contains the claims granted to a user.</returns>
        public async override Task<IList<Claim>> GetClaimsAsync(TUser user, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            AssertNotNull(user, nameof(user));

            var snapShot = await _usersClaims.WhereEqualTo("UserId", user.Id)
                .GetSnapshotAsync(cancellationToken)
                .ConfigureAwait(false);
            return snapShot.Documents
                .Select(d => new Claim(d.GetValue<string>("Type"), d.GetValue<string>("Value")))
                .ToList();
        }

        /// <summary>
        /// Adds the <paramref name="claims"/> given to the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to add the claim to.</param>
        /// <param name="claims">The claim to add to the user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public override async Task AddClaimsAsync(TUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            AssertNotNull(user, nameof(user));
            AssertNotNull(claims, nameof(claims));

            foreach (var claim in claims)
            {
                Dictionary<string, object> dictionary = ClaimsToDictionary(user, claim);
                await _usersClaims.AddAsync(dictionary, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Replaces the <paramref name="claim"/> on the specified <paramref name="user"/>, with the <paramref name="newClaim"/>.
        /// </summary>
        /// <param name="user">The user to replace the claim on.</param>
        /// <param name="claim">The claim replace.</param>
        /// <param name="newClaim">The new claim replacing the <paramref name="claim"/>.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public override Task ReplaceClaimAsync(TUser user, Claim claim, Claim newClaim, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            AssertNotNull(user, nameof(user));
            AssertNotNull(claim, nameof(claim));
            AssertNotNull(newClaim, nameof(newClaim));

            return _db.RunTransactionAsync(async transaction =>
            {
                var snapShot = await _usersClaims.WhereEqualTo("UserId", user.Id)
                    .WhereEqualTo("Type", claim.Type)
                    .GetSnapshotAsync(cancellationToken)
                    .ConfigureAwait(false);
                var document = snapShot.Documents.First();
                transaction.Update(document.Reference, ClaimsToDictionary(user, newClaim));
            }, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Removes the <paramref name="claims"/> given from the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to remove the claims from.</param>
        /// <param name="claims">The claim to remove.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public override Task RemoveClaimsAsync(TUser user, IEnumerable<Claim> claims, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            AssertNotNull(user, nameof(user));
            AssertNotNull(claims, nameof(claims));

            return _db.RunTransactionAsync(async transaction =>
            {
                foreach(var claim in claims)
                {
                    var snapShot = await _usersClaims.WhereEqualTo("UserId", user.Id)
                        .WhereEqualTo("Type", claim.Type)
                        .GetSnapshotAsync(cancellationToken)
                        .ConfigureAwait(false);
                    var document = snapShot.Documents.First();
                    transaction.Delete(document.Reference);
                }
            }, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Adds the <paramref name="login"/> given to the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to add the login to.</param>
        /// <param name="login">The login to add to the user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public override Task AddLoginAsync(TUser user, UserLoginInfo login,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            AssertNotNull(user, nameof(user));
            AssertNotNull(login, nameof(login));

            var dictionary = new Dictionary<string, object>
            {
                { "UserId", user.Id },
                { "LoginProvider", login.LoginProvider },
                { "ProviderKey", login.ProviderKey },
                { "ProviderDisplayName", login.ProviderDisplayName }
            };
            return _usersLogins.AddAsync(dictionary, cancellationToken);
        }

        /// <summary>
        /// Removes the <paramref name="loginProvider"/> given from the specified <paramref name="user"/>.
        /// </summary>
        /// <param name="user">The user to remove the login from.</param>
        /// <param name="loginProvider">The login to remove from the user.</param>
        /// <param name="providerKey">The key provided by the <paramref name="loginProvider"/> to identify a user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        public override Task RemoveLoginAsync(TUser user, string loginProvider, string providerKey,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            AssertNotNull(user, nameof(user));
            AssertNotNull(loginProvider, nameof(loginProvider));
            AssertNotNull(providerKey, nameof(providerKey));

            return _db.RunTransactionAsync(async transaction =>
            {
                var snapShot = await _usersLogins.WhereEqualTo("UserId", user.Id)
                    .WhereEqualTo("LoginProvider", loginProvider)
                    .WhereEqualTo("ProviderKey", providerKey)
                    .GetSnapshotAsync(cancellationToken)
                    .ConfigureAwait(false);
                var document = snapShot.Documents.First();
                transaction.Delete(document.Reference);
            }, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Retrieves the associated logins for the specified <param ref="user"/>.
        /// </summary>
        /// <param name="user">The user whose associated logins to retrieve.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> for the asynchronous operation, containing a list of <see cref="UserLoginInfo"/> for the specified <paramref name="user"/>, if any.
        /// </returns>
        public async override Task<IList<UserLoginInfo>> GetLoginsAsync(TUser user, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            AssertNotNull(user, nameof(user));

            var snapShot = await _usersLogins.WhereEqualTo("UserId", user.Id)
                .GetSnapshotAsync(cancellationToken)
                .ConfigureAwait(false);
            var documents = snapShot.Documents;
            var list = new List<UserLoginInfo>(documents.Count);
            foreach(var doc in documents)
            {
                list.Add(new UserLoginInfo(doc.GetValue<string>("LoginProvider"),
                    doc.GetValue<string>("ProviderKey"),
                    doc.GetValue<string>("ProviderDisplayName")));
            }
            return list;
        }

        /// <summary>
        /// Retrieves the user associated with the specified login provider and login provider key.
        /// </summary>
        /// <param name="loginProvider">The login provider who provided the <paramref name="providerKey"/>.</param>
        /// <param name="providerKey">The key provided by the <paramref name="loginProvider"/> to identify a user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> for the asynchronous operation, containing the user, if any which matched the specified login provider and key.
        /// </returns>
        public async override Task<TUser> FindByLoginAsync(string loginProvider, string providerKey,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            AssertNotNullOrEmpty(loginProvider, nameof(loginProvider));
            AssertNotNullOrEmpty(providerKey, nameof(providerKey));

            var snapShot = await _usersLogins.WhereEqualTo("LoginProvider", loginProvider)
                .WhereEqualTo("ProviderKey", providerKey)
                .GetSnapshotAsync(cancellationToken)
                .ConfigureAwait(false);
            var document = snapShot.Documents.FirstOrDefault();
            if (document != null)
            {
                var userId = document.GetValue<string>("UserId");
                return await FindByIdAsync(userId, cancellationToken)
                    .ConfigureAwait(false);
            }
            return null;
        }

        /// <summary>
        /// Gets the user, if any, associated with the specified, normalized email address.
        /// </summary>
        /// <param name="normalizedEmail">The normalized email address to return the user for.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The task object containing the results of the asynchronous lookup operation, the user if any associated with the specified normalized email address.
        /// </returns>
        public override async Task<TUser> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            AssertNotNullOrEmpty(normalizedEmail, nameof(normalizedEmail));
            
            var snapShot = await _users.WhereEqualTo("NormalizedEmail", normalizedEmail)
                .GetSnapshotAsync(cancellationToken)
                .ConfigureAwait(false);
            var document = snapShot.Documents.FirstOrDefault();
            if (document != null)
            {
                return Map.FromDictionary<TUser>(document.ToDictionary());
            }
            return null;
        }

        /// <summary>
        /// Retrieves all users with the specified claim.
        /// </summary>
        /// <param name="claim">The claim whose users should be retrieved.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>
        /// The <see cref="Task"/> contains a list of users, if any, that contain the specified claim. 
        /// </returns>
        public async override Task<IList<TUser>> GetUsersForClaimAsync(Claim claim, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            AssertNotNull(claim, nameof(claim));

            var snapShot = await _usersClaims.WhereEqualTo("Type", claim.Type)
                .WhereEqualTo("Value", claim.Value)
                .GetSnapshotAsync(cancellationToken)
                .ConfigureAwait(false);
            var documents = snapShot.Documents;
            var list = new List<TUser>(documents.Count);
            foreach(var document in documents)
            {
                var user = await FindByIdAsync(document.GetValue<string>("UserId"))
                    .ConfigureAwait(false);
                if (user != null)
                {
                    list.Add(user);
                }
            }
            
            return list.ToList();
        }

        /// <summary>
        /// Return a user login with the matching userId, provider, providerKey if it exists.
        /// </summary>
        /// <param name="userId">The user's id.</param>
        /// <param name="loginProvider">The login provider name.</param>
        /// <param name="providerKey">The key provided by the <paramref name="loginProvider"/> to identify a user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The user login if it exists.</returns>
        internal Task<TUserLogin> FindUserLoginInternalAsync(string userId, string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            return FindUserLoginAsync(userId, loginProvider, providerKey, cancellationToken);
        }

        /// <summary>
        /// Return a user login with  provider, providerKey if it exists.
        /// </summary>
        /// <param name="loginProvider">The login provider name.</param>
        /// <param name="providerKey">The key provided by the <paramref name="loginProvider"/> to identify a user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The user login if it exists.</returns>
        internal Task<TUserLogin> FindUserLoginInternalAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            return FindUserLoginAsync(loginProvider, providerKey, cancellationToken);
        }

        /// <summary>
        /// Get user tokens
        /// </summary>
        /// <param name="user">The token owner.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>User tokens.</returns>
        internal Task<List<TUserToken>> GetUserTokensInternalAsync(TUser user, CancellationToken cancellationToken)
        {
            return GetUserTokensAsync(user, cancellationToken);
        }

        /// <summary>
        /// Save user tokens.
        /// </summary>
        /// <param name="user">The tokens owner.</param>
        /// <param name="tokens">Tokens to save</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns></returns>
        internal Task SaveUserTokensInternalAsync(TUser user, IEnumerable<TUserToken> tokens, CancellationToken cancellationToken)
        {
            return SaveUserTokensAsync(user, tokens, cancellationToken);
        }

        /// <summary>
        /// Return a user with the matching userId if it exists.
        /// </summary>
        /// <param name="userId">The user's id.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The user if it exists.</returns>
        protected override Task<TUser> FindUserAsync(string userId, CancellationToken cancellationToken)
        {
            return FindByIdAsync(userId.ToString(), cancellationToken);
        }

        /// <summary>
        /// Return a user login with the matching userId, provider, providerKey if it exists.
        /// </summary>
        /// <param name="userId">The user's id.</param>
        /// <param name="loginProvider">The login provider name.</param>
        /// <param name="providerKey">The key provided by the <paramref name="loginProvider"/> to identify a user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The user login if it exists.</returns>
        protected override async Task<TUserLogin> FindUserLoginAsync(string userId, string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            var snapShot = await _usersLogins.WhereEqualTo("UserId", userId)
                .WhereEqualTo("LoginProvider", loginProvider)
                .WhereEqualTo("ProviderKey", providerKey)
                .GetSnapshotAsync(cancellationToken)
                .ConfigureAwait(false);
            var document = snapShot.Documents.FirstOrDefault();
            if (document != null)
            {
                return Map.FromDictionary<TUserLogin>(document.ToDictionary());
            }
            return default;
        }

        /// <summary>
        /// Return a user login with  provider, providerKey if it exists.
        /// </summary>
        /// <param name="loginProvider">The login provider name.</param>
        /// <param name="providerKey">The key provided by the <paramref name="loginProvider"/> to identify a user.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>The user login if it exists.</returns>
        protected override async Task<TUserLogin> FindUserLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            var snapShot = await _usersLogins.WhereEqualTo("LoginProvider", loginProvider)
                    .WhereEqualTo("ProviderKey", providerKey)
                    .GetSnapshotAsync(cancellationToken)
                    .ConfigureAwait(false);
            var document = snapShot.Documents.FirstOrDefault();
            if (document != null)
            {
                return Map.FromDictionary<TUserLogin>(document.ToDictionary());
            }
            return default;
        }

        /// <summary>
        /// Get user tokens
        /// </summary>
        /// <param name="user">The token owner.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns>User tokens.</returns>
        protected override async Task<List<TUserToken>> GetUserTokensAsync(TUser user, CancellationToken cancellationToken)
        {
            var snapShot = await _usersTokens.WhereEqualTo("UserId", user.Id)
                .GetSnapshotAsync(cancellationToken)
                .ConfigureAwait(false);
            return snapShot.Documents
                .Select(d => Map.FromDictionary<TUserToken>(d.ToDictionary()))
                .ToList();
        }

        /// <summary>
        /// Save user tokens.
        /// </summary>
        /// <param name="user">The tokens owner.</param>
        /// <param name="tokens">Tokens to save</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to propagate notifications that the operation should be canceled.</param>
        /// <returns></returns>
        protected override async Task SaveUserTokensAsync(TUser user, IEnumerable<TUserToken> tokens, CancellationToken cancellationToken)
        {
            var snapShop = await _usersTokens.WhereEqualTo("UserId", user.Id)
                .GetSnapshotAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var document in snapShop.Documents)
            {
                await _usersTokens.Document(document.Id).DeleteAsync(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            foreach (var token in tokens)
            {
                var dictionary = Map.ToDictionary(token);
                dictionary["UserId"] = user.Id;
                await _usersTokens.AddAsync(dictionary, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        protected virtual Dictionary<string, object> ClaimsToDictionary(TUser user, Claim claim)
        {
            return new Dictionary<string, object>
                {
                    { "UserId",  user.Id },
                    { "Type", claim.Type },
                    { "Value", claim.Value }
                };
        }
    }
}
