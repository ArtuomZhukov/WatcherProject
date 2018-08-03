using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WatcherProject
{
    public class PeopleData
    {
        public List<string> Name { get; set; } = new List<string>();
        public List<string> Face { get; set; } = new List<string>(); 
        public List<int> NameId { get; set; } = new List<int>();
        public List<int> DeletedNamesId { get; set; } = new List<int>();
        public void Add(string name, string face)
        {
            int index = Name.IndexOf(name) + 1;
            if (index == 0)
            {
                if (DeletedNamesId.Count > 0)
                {
                    Name[DeletedNamesId[0]] = name;
                    NameId.Add(DeletedNamesId[0] + 1);
                    DeletedNamesId.RemoveAt(0);
                }
                else
                {
                    Name.Add(name);
                    NameId.Add(Name.Count());
                }
            }
            else
            {
                NameId.Add(index);
            }
            Face.Add(face);
        }
    }
}
