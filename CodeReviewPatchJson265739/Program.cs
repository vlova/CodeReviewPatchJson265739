using System;
using System.Text.Json;

namespace CodeReviewPatchJson265739
{
    class Program
    {
        static void Main(string[] args)
        {
            string original = @"{""foo"":[1,2,3],""parent"":{""childInt"":1},""bar"":""example""}";
            string patch = @"{""foo"":[9,8,7],""parent"":{""childInt"":9,""childString"":""woot!""},""bar"":null}";
            Console.WriteLine(original);

            // change this value to see the different types of patching method
            bool addPropertyIfNotExists = false;

            // patch it!
            var expandoObject = JsonDocument.Parse(original).DynamicUpdate(patch, new JsonExtensions.DynamicUpdateOptions
            {
                AddPropertyIfNotExists = addPropertyIfNotExists
            });
            Console.WriteLine(JsonSerializer.Serialize(expandoObject));
        }
    }
}
