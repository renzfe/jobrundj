using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace jobrundj.Console
{
    class NamespaceList : HashSet<string>
    {
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            var enumerator = this.GetEnumerator();
            foreach (string ns in this)
            {
                sb.AppendLine($"using {ns};");
            }

            return sb.ToString();
        }
    }
}
