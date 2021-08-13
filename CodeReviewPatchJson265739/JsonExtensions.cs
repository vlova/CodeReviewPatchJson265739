using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Text.Json;

namespace CodeReviewPatchJson265739
{
    public static class JsonExtensions
    {
        public static IDictionary<string, object> GetPatched(
            this JsonDocument toPatch,
            string patch,
            PatchOptions patchOptions,
            JsonDocumentOptions jsonDocumentOptions = default)
        {
            using var doc = JsonDocument.Parse(patch, jsonDocumentOptions);
            return GetPatched(toPatch, doc, patchOptions);
        }

        public static IDictionary<string, object> GetPatched(
            this JsonDocument toPatch,
            JsonDocument patch,
            PatchOptions patchOptions)
        {
            if (patch == null) throw new ArgumentNullException(nameof(patch));

            return GetPatched(toPatch.RootElement.Clone(), patch.RootElement.Clone(), patchOptions);
        }

        private static IDictionary<string, object> GetPatched(
            JsonElement toPatch,
            JsonElement patch,
            PatchOptions patchOptions)
        {
            var patched = ToExpandoObject(toPatch);
            Patch(patched, patch, patchOptions);
            return patched;
        }

        private static void Patch(
            IDictionary<string, object> toPatch,
            JsonElement patch,
            PatchOptions patchOptions)
        {
            patchOptions ??= new PatchOptions();

            if (patch.ValueKind != JsonValueKind.Object)
                throw new NotSupportedException("Only objects are supported.");

            foreach (var patchChildProp in patch.EnumerateObject())
            {
                var propertyName = patchChildProp.Name;
                var toPatchHasProperty = toPatch.ContainsKey(propertyName);
                if (!toPatchHasProperty && !patchOptions.AddPropertyIfNotExists) continue;

                var toPatchChild = GetJsonProperty(toPatch, propertyName);
                toPatch[propertyName] = GetPatched(
                    toPatchChild, patchChildProp.Value, propertyName,
                    patchOptions);
            }
        }

        private static JsonElement? GetJsonProperty(IDictionary<string, object> entity, string propertyName)
        {
            entity.TryGetValue(propertyName, out var oldValue);

            if (oldValue == null)
                return null;

            if (!oldValue.GetType().IsAssignableTo(typeof(JsonElement)))
                throw new ArgumentException($"Type mismatch. Must be {nameof(JsonElement)}.", nameof(entity));

            return (JsonElement)oldValue;
        }

        private static object GetPatched(
            JsonElement? toPatch,
            JsonElement patch,
            string propertyName,
            PatchOptions patchOptions)
        {
            if (toPatch == null) return patch;
            var oldElement = (JsonElement)toPatch;

            if (patchOptions.UseTypeValidation && !IsValidType(oldElement, patch))
                throw new ArgumentException($"Type mismatch. The property '{propertyName}' must be of type '{oldElement.ValueKind}'.", nameof(patch));

            if (oldElement.ValueKind == JsonValueKind.Object)
            {
                return GetPatched(oldElement, patch, patchOptions);
            }

            return patch;
        }

        public class PatchOptions
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

            if (oldElement.ValueKind == JsonValueKind.True && newElement.ValueKind == JsonValueKind.False) return true;

            if (oldElement.ValueKind == JsonValueKind.False && newElement.ValueKind == JsonValueKind.True) return true;

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
