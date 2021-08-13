using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text.Json;

namespace CodeReviewPatchJson265739
{
    public static class JsonExtensions
    {
        public static void DynamicUpdate(
            this IDictionary<string, object> entity,
            string patchJson,
            DynamicUpdateOptions updateOptions,
            JsonDocumentOptions jsonDocumentOptions = default)
        {
            using JsonDocument doc = JsonDocument.Parse(patchJson, jsonDocumentOptions);
            DynamicUpdate(entity, doc, updateOptions);
        }

        public static void DynamicUpdate(
            this IDictionary<string, object> entity,
            JsonDocument doc,
            DynamicUpdateOptions updateOptions)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));

            var rootElement = doc.RootElement.Clone();
            DynamicUpdate(entity, rootElement, updateOptions);
        }

        public static void DynamicUpdate(
            this IDictionary<string, object> entity,
            JsonElement rootElement,
            DynamicUpdateOptions updateOptions)
        {
            updateOptions ??= new DynamicUpdateOptions();

            if (rootElement.ValueKind != JsonValueKind.Object)
                throw new NotSupportedException("Only objects are supported.");

            foreach (JsonProperty jsonProperty in rootElement.EnumerateObject())
            {
                string propertyName = jsonProperty.Name;
                var hasProperty = entity.ContainsKey(propertyName);
                JsonElement newElement = rootElement.GetProperty(propertyName);
                JsonElement? oldElement = GetJsonProperty(entity, propertyName);
                if (!hasProperty && !updateOptions.AddPropertyIfNotExists) continue;
                entity[propertyName] = GetNewValue(
                    oldElement, newElement, propertyName,
                    updateOptions);
            }
        }

        private static JsonElement? GetJsonProperty(IDictionary<string, object> entity, string propertyName)
        {
            entity.TryGetValue(propertyName, out object oldValue);

            if (oldValue == null)
                return null;

            if (!oldValue.GetType().IsAssignableTo(typeof(JsonElement)))
                throw new ArgumentException($"Type mismatch. Must be {nameof(JsonElement)}.", nameof(entity));

            return (JsonElement)oldValue;
        }

        private static object GetNewValue(
            JsonElement? oldElementNullable,
            JsonElement newElement,
            string propertyName,
            DynamicUpdateOptions updateOptions)
        {
            if (oldElementNullable == null) return newElement;
            JsonElement oldElement = (JsonElement)oldElementNullable;

            // type validation
            if (updateOptions.UseTypeValidation && !IsValidType(oldElement, newElement))
                throw new ArgumentException($"Type mismatch. The property '{propertyName}' must be of type '{oldElement.ValueKind}'.", nameof(newElement));

            // recursively go down the tree for objects
            if (oldElement.ValueKind == JsonValueKind.Object)
            {
                var oldObject = ToExpandoObject(oldElement);
                DynamicUpdate(oldObject, newElement, updateOptions);
                return oldObject;
            }

            return newElement;
        }

        public class DynamicUpdateOptions
        {
            public bool AddPropertyIfNotExists { get; set; } = false;

            public bool UseTypeValidation { get; set; } = true;
        }

        private static ExpandoObject ToExpandoObject(JsonElement jsonElement)
        {
            var obj = new ExpandoObject();
            foreach (var property in jsonElement.EnumerateObject())
            {
                (obj as IDictionary<string, object>)[property.Name] = property.Value;
            }

            return obj;
        }

        private static bool IsValidType(JsonElement oldElement, JsonElement newElement)
        {
            if (newElement.ValueKind == JsonValueKind.Null) return true;

            // 'true' --> 'false'
            if (oldElement.ValueKind == JsonValueKind.True && newElement.ValueKind == JsonValueKind.False) return true;
            // 'false' --> 'true'
            if (oldElement.ValueKind == JsonValueKind.False && newElement.ValueKind == JsonValueKind.True) return true;

            // type validation
            return (oldElement.ValueKind == newElement.ValueKind);
        }

        private static bool IsValidJsonPropertyName(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;

            // this is validation for our specific use case (C#)
            // note that the official docs don't prohibit this though.
            // https://datatracker.ietf.org/doc/html/rfc7159
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsLetterOrDigit(value[i])) continue;
                switch (value[i])
                {
                    case '-':
                    case '_':
                    default:
                        break;
                }
            }

            return true;
        }
    }
}
