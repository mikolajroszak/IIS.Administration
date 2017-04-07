﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


namespace Microsoft.IIS.Administration.WebServer.CentralCertificates
{
    using Core;
    using Core.Utils;
    using Newtonsoft.Json.Linq;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Dynamic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography.X509Certificates;
    using System.Security.Principal;
    using System.Threading.Tasks;
    using Win32;
    using Win32.SafeHandles;

    class CentralCertHelper
    {
        public static object ToJsonModel()
        {
            var ccs = Startup.CentralCertificateStore;

            dynamic obj = new ExpandoObject();
            obj.enabled = ccs.Enabled;
            obj.name = Constants.STORE_NAME;
            obj.id = new CentralCertId().Uuid;

            if (ccs.Enabled) {
                obj.path = ccs.PhysicalPath;
                obj.identity = new {
                    username = ccs.UserName
                };
            };

            return Core.Environment.Hal.Apply(Defines.Resource.Guid, obj);
        }

        public static void Update(dynamic model)
        {
            if (model == null) {
                throw new ApiArgumentException("model");
            }

            if (!(model is JObject)) {
                throw new ApiArgumentException("model", ApiArgumentException.EXPECTED_OBJECT);
            }

            //
            // Validate model and extract values
            bool? enabled = DynamicHelper.To<bool>(model.enabled);
            string path = DynamicHelper.Value(model.path);
            string privateKeyPassword = DynamicHelper.Value(model.private_key_password);
            string username = null, password = null;

            if (model.identity != null) {
                dynamic identity = model.identity;

                if (!(identity is JObject)) {
                    throw new ApiArgumentException("identity", ApiArgumentException.EXPECTED_OBJECT);
                }

                username = DynamicHelper.Value(identity.username);
                password = DynamicHelper.Value(identity.password);
            }

            if (enabled.HasValue && enabled.Value) {
                if (string.IsNullOrEmpty(username)) {
                    throw new ApiArgumentException("identity.username");
                }

                if (string.IsNullOrEmpty(password)) {
                    throw new ApiArgumentException("identity.password");
                }
            }

            var ccs = Startup.CentralCertificateStore;

            //
            // Update ccs
            if (username != null) {
                ccs.UserName = username;
            }

            if (password != null) {
                ccs.EncryptedPassword = Convert.ToBase64String(Crypto.Encrypt(password));
            }

            if (path != null) {
                ccs.PhysicalPath = path;
            }

            if (privateKeyPassword != null) {
                ccs.EncryptedPrivateKeyPassword = Convert.ToBase64String(Crypto.Encrypt(privateKeyPassword));
            }

            if (enabled.HasValue) {
                ccs.Enabled = enabled.Value;
            }
        }

        public static bool TestConnection()
        {
            var ccs = Startup.CentralCertificateStore;

            if (!ccs.Enabled) {
                return false;
            }

            try {
                using (SafeAccessTokenHandle ccsUser = LogonAsCcsUser()) {
                    WindowsIdentity.RunImpersonated(ccsUser, () => {
                        Directory.GetFiles(ccs.PhysicalPath);
                    });
                }
            }
            catch {
                return false;
            }
            return true;
        }

        public static async Task<IEnumerable<X509Certificate2>> GetCertificates()
        {
            var ccs = Startup.CentralCertificateStore;

            using (SafeAccessTokenHandle ccsUser = LogonAsCcsUser()) {
                return await Task.Run(() => {
                    return WindowsIdentity.RunImpersonated(ccsUser, () => {
                        return Directory.GetFiles(ccs.PhysicalPath).Select(f => {

                            X509Certificate2 cert = !string.IsNullOrEmpty(ccs.EncryptedPrivateKeyPassword) ?
                                                        new X509Certificate2(f, Crypto.Decrypt(Convert.FromBase64String(ccs.EncryptedPrivateKeyPassword))) :
                                                        new X509Certificate2(f);



                            cert.FriendlyName = Path.GetFileNameWithoutExtension(f);
                            return cert;
                        });
                    });
                });
            }
        }

        private static SafeAccessTokenHandle LogonAsCcsUser()
        {
            var ccs = Startup.CentralCertificateStore;

            if (!ccs.Enabled) {
                throw new InvalidOperationException();
            }

            SafeAccessTokenHandle token = null;

            string[] parts = ccs.UserName.Split('\\');
            string domain = null, username = null;

            if (parts.Length > 1) {
                domain = parts[0];
                username = parts[1];
            }
            else {
                domain = ".";
                username = parts[0];
            }

            bool loggedOn = Interop.LogonUserExExW(username,
                domain,
                Crypto.Decrypt(Convert.FromBase64String(ccs.EncryptedPassword)),
                Interop.LOGON32_LOGON_INTERACTIVE,
                Interop.LOGON32_PROVIDER_DEFAULT,
                IntPtr.Zero,
                out token,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            if (!loggedOn) {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return token;
        }
    }
}
