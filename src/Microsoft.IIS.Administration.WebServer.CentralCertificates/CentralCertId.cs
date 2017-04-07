﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


namespace Microsoft.IIS.Administration.WebServer.CentralCertificates
{
    using System;

    public class CentralCertId
    {
        private const string PURPOSE = "WebServer.CentralCertificates";

        public string Uuid { get; private set; }

        public CentralCertId()
        {
            Uuid = Core.Utils.Uuid.Encode($"", PURPOSE);
        }

        public CentralCertId(string uuid)
        {
            if (string.IsNullOrEmpty(uuid)) {
                throw new ArgumentNullException(nameof(uuid));
            }

            Uuid = uuid;
        }
    }
}