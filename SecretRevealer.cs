using System;
using Microsoft.Extensions.Options;

using System.Collections.Generic;

using HelpjuiceConverter.Entities;

namespace HelpjuiceConverter
{
    class SecretRevealer : ISecretRevealer
    {
        private readonly Secrets _secrets;

        public SecretRevealer(IOptions<Secrets> secrets)
        {
            // We want to know if secrets is null so we throw an exception if it is
            _secrets = secrets.Value ?? throw new ArgumentNullException(nameof(secrets));
        }

        public Dictionary<string, string> Reveal()
        {
            var result = new Dictionary<string, string>();
            result.Add("JBASE", _secrets.JBASE_API_KEY);
            result.Add("ZUMASYS", _secrets.ZUMASYS_API_KEY);

            return result;
        }
    }
}