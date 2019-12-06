using System.Collections.Generic;

namespace HelpjuiceConverter
{
    interface ISecretRevealer
    {
        Dictionary<string, string> Reveal();
    }

}