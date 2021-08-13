using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text.Json;

namespace CodeReviewPatchJson265739
{
    public static class JsonExtensions
    {
        public static object DynamicUpdate(
            this IDictionary<string, object> entity,
            string patchJson,
            bool addPropertyIfNotExists = false,
            bool useTypeValidation = true,
            JsonDocumentOptions options = default)
        {
            using JsonDocument doc = JsonDocument.Parse(patchJson, options);
            return DynamicUpdate(entity, doc, addPropertyIfNotExists, useTypeValidation);
        }

        public static object DynamicUpdate(
            this IDictionary<string, object> entity,
            JsonDocument doc,
            bool addPropertyIfNotExists = false,
            bool useTypeValidation = true)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));

            var rootElement = doc.RootElement.Clone();
            return DynamicUpdate(entity, rootElement, addPropertyIfNotExists, useTypeValidation);
        }

        public static object DynamicUpdate(
            this IDictionary<string, object> entity,
            JsonElement rootElement,
            bool addPropertyIfNotExists = false,
            bool useTypeValidation = true)
        {
            if (rootElement.ValueKind != JsonValueKind.Object)
                throw new NotSupportedException("Only objects are supported.");

            foreach (JsonProperty jsonProperty in rootElement.EnumerateObject())
            {
                string propertyName = jsonProperty.Name;
                JsonElement newElement = rootElement.GetProperty(propertyName);
                bool hasProperty = entity.TryGetValue(propertyName, out object oldValue);

                // sanity checks
                JsonElement? oldElement = null;
                if (oldValue != null)
                {
                    if (!oldValue.GetType().IsAssignableTo(typeof(JsonElement)))
                        throw new ArgumentException($"Type mismatch. Must be {nameof(JsonElement)}.", nameof(entity));
                    oldElement = (JsonElement)oldValue;
                }
                if (!hasProperty && !addPropertyIfNotExists) continue;
                entity[propertyName] = GetNewValue(
                    oldElement, newElement, propertyName,
                    addPropertyIfNotExists, useTypeValidation);
            }

            return entity;
        }

        private static object GetNewValue(
            JsonElement? oldElementNullable,
            JsonElement newElement,
            string propertyName,
            bool addPropertyIfNotExists,
            bool useTypeValidation)
        {
            if (oldElementNullable == null) return newElement;
            JsonElement oldElement = (JsonElement)oldElementNullable;

            // type validation
            if (useTypeValidation && !IsValidType(oldElement, newElement))
                throw new ArgumentException($"Type mismatch. The property '{propertyName}' must be of type '{oldElement.ValueKind}'.", nameof(newElement));

            // recursively go down the tree for objects
            if (oldElement.ValueKind == JsonValueKind.Object)
            {
                return DynamicUpdate(ToExpandoObject(oldElement), newElement, addPropertyIfNotExists, useTypeValidation);
            }

            return newElement;
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
