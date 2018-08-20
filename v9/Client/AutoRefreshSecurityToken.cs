﻿// =====================================================================
//  This file is part of the Microsoft Dynamics CRM SDK code samples.
//
//  Copyright (C) Microsoft Corporation.  All rights reserved.
//
//  This source code is intended only as a supplement to Microsoft
//  Development Tools and/or on-line documentation.  See these other
//  materials for detailed information regarding Microsoft code samples.
//
//  THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY
//  KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
//  IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
//  PARTICULAR PURPOSE.
// =====================================================================
namespace Microsoft.Pfe.Xrm
{
    using System;
    using System.ServiceModel;
    using System.Text;
    using Microsoft.Xrm.Sdk.Client;
    using Microsoft.Pfe.Xrm.Diagnostics;

    /// <summary>
    /// Class that handles renewing the <see cref="SecurityTokenResponse"/> if expired
    /// </summary>
    public sealed class AutoRefreshSecurityToken<TProxy, TService>
        where TProxy : ServiceProxy<TService>
        where TService : class
    {        
        private TProxy _proxy;

        /// <summary>
        /// Instantiates an instance of the proxy class
        /// </summary>
        /// <param name="proxy">Proxy that will be used to authenticate the user</param>
        public AutoRefreshSecurityToken(TProxy proxy)
        {
            if (proxy == null)
            {
                throw new ArgumentNullException("proxy");
            }

            this._proxy = proxy;
        }

        /// <summary>
        /// Prepares authentication before authenticated
        /// </summary>
        public void PrepareCredentials()
        {
            if (this._proxy.ClientCredentials == null)
            {
                return;
            }

            switch (this._proxy.ServiceConfiguration.AuthenticationType)
            {
                case AuthenticationProviderType.ActiveDirectory:
                    this._proxy.ClientCredentials.UserName.UserName = null;
                    this._proxy.ClientCredentials.UserName.Password = null;
                    break;
                case AuthenticationProviderType.Federation:
                case AuthenticationProviderType.OnlineFederation:
                case AuthenticationProviderType.LiveId:
                    this._proxy.ClientCredentials.Windows.ClientCredential = null;
                    break;
                default:
                    return;
            }
        }

        /// <summary>
        /// Renews the token for non-AD scenarios (if it is near expiration or has expired)
        /// </summary>
        public void RenewTokenIfRequired()
        {
            if (this._proxy.ServiceConfiguration.AuthenticationType != AuthenticationProviderType.ActiveDirectory
                && this._proxy.SecurityTokenResponse != null)
            {
                DateTime? expiresOn = this._proxy.SecurityTokenResponse.Response.Lifetime.Expires;

                if (DateTime.UtcNow.AddMinutes(15) >= expiresOn)
                {
                    string expiresOnValue = expiresOn.HasValue 
                        ? expiresOn.Value.ToString("u") 
                        : String.Empty;

                    XrmCoreEventSource.Log.SecurityTokenRefreshRequired(expiresOnValue);
  
                    try
                    {
                        this._proxy.Authenticate();
                    }
                    catch (Exception ex)
                    {
                        StringBuilder messageBuilder = ex.ToErrorMessageString();

                        XrmCoreEventSource.Log.SecurityTokenRefreshFailure(expiresOnValue, messageBuilder.ToString());

                        if (ex is CommunicationException
                            && (this._proxy.SecurityTokenResponse == null
                                || DateTime.UtcNow >= this._proxy.SecurityTokenResponse.Response.Lifetime.Expires))
                        {
                            throw;
                        }

                        // Ignore the exception 
                    }
                }
            }
        }
    }
}
