﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


namespace Microsoft.IIS.Administration.Certificates
{
    using Core;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;

    public class WindowsCertificateStore : ICertificateStore
    {
        private string _name;
        private IEnumerable<string> _claims;

        public WindowsCertificateStore(string name, IEnumerable<string> claims)
        {
            _name = name;
            _claims = claims;
        }

        public bool IsWindowsStore {
            get {
                return true;
            }
        }

        public string Name {
            get {
                return _name;
            }
        }

        public IEnumerable<string> Claims {
            get {
                return _claims;
            }
        }

        public static bool Exists(string name)
        {
            using (X509Store store = new X509Store(name, StoreLocation.LocalMachine)) {
                try {
                    store.Open(OpenFlags.OpenExistingOnly);
                }
                catch (CryptographicException) {
                    return false;
                }
            }

            return true;
        }

        public async Task<IEnumerable<ICertificate>> GetCertificates()
        {
            EnsureAccess(CertificateAccess.Read);

            var certs = new List<ICertificate>();

            return await Task.Run(() => {
                using (X509Store store = new X509Store(Name, StoreLocation.LocalMachine)) {
                    store.Open(OpenFlags.OpenExistingOnly);

                    foreach (X509Certificate2 cert in store.Certificates) {
                        certs.Add(new Certificate(cert, this));
                        cert.Dispose();
                    }

                    return certs;
                }
            });
        }

        public async Task<ICertificate> GetCertificate(string thumbprint)
        {
            EnsureAccess(CertificateAccess.Read);

            foreach (var cert in await GetCertificates()) {
                if (cert.Thumbprint.Equals(thumbprint)) {
                    return cert;
                }
            }

            return null;
        }

        //
        // IDisposable
        public Stream GetContent(ICertificate certificate, bool persistKey, string password)
        {
            EnsureAccess(CertificateAccess.Read | CertificateAccess.Export);

            X509Certificate2 target = null;
            Stream stream = null;

            using (X509Store store = new X509Store(Name, StoreLocation.LocalMachine)) {
                store.Open(OpenFlags.OpenExistingOnly);

                foreach (X509Certificate2 cert in store.Certificates) {
                    if (cert.Thumbprint.Equals(certificate.Thumbprint)) {
                        target = cert;
                    }
                    else {
                        cert.Dispose();
                    }
                }
            }

            X509ContentType contentType = persistKey ? X509ContentType.Pfx : X509ContentType.Cert;

            if (target != null) {
                byte[] bytes = password == null ? target.Export(contentType) : target.Export(contentType, password);
                stream = new MemoryStream(bytes);
                target.Dispose();
            }

            return stream;
        }

        private bool IsAccessAllowed(CertificateAccess access)
        {
            return ((!access.HasFlag(CertificateAccess.Read) || _claims.Contains("Read", StringComparer.OrdinalIgnoreCase))
                        && (!access.HasFlag(CertificateAccess.Delete) || _claims.Contains("Delete", StringComparer.OrdinalIgnoreCase))
                        && (!access.HasFlag(CertificateAccess.Create) || _claims.Contains("Create", StringComparer.OrdinalIgnoreCase))
                        && (!access.HasFlag(CertificateAccess.Export) || _claims.Contains("Export", StringComparer.OrdinalIgnoreCase)));
        }

        private void EnsureAccess(CertificateAccess access)
        {
            if (!IsAccessAllowed(access)) {
                throw new ForbiddenArgumentException("certificate_store");
            }
        }
    }
}
