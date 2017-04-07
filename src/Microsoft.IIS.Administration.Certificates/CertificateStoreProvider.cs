﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


namespace Microsoft.IIS.Administration.Certificates
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class CertificateStoreProvider : ICertificateStoreProvider
    {
        List<ICertificateStore> _stores = new List<ICertificateStore>();

        public IEnumerable<ICertificateStore> Stores {
            get {
                return _stores.AsEnumerable();
            }
        }

        public void AddStore(ICertificateStore store)
        {
            if (GetStore(store.Name) == null) {
                _stores.Add(store);
            }
        }

        public ICertificateStore GetStore(string storeName)
        {
            return _stores.FirstOrDefault(s => s.Name.Equals(storeName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
