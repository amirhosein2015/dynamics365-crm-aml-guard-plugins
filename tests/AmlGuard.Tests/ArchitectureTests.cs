using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AmlGuard.Plugins.Domain;
using Xunit;

namespace AmlGuard.Tests
{
    // Guards decision D1: business logic stays free of the Dataverse SDK so it can be unit tested
    // without any mocking. It inspects declared types (fields, properties, method signatures, locals);
    // it cannot see a type that is only used inside a method call, so it is a tripwire, not a proof.
    public class ArchitectureTests
    {
        private const string DomainNamespace = "AmlGuard.Plugins.Domain";

        private const BindingFlags AllDeclaredMembers =
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        [Fact]
        public void DomainTypes_DoNotDeclareOrStoreDataverseSdkTypes()
        {
            var domainTypes = typeof(NameNormalizer).Assembly
                .GetTypes()
                .Where(type => type.Namespace != null && type.Namespace.StartsWith(DomainNamespace, StringComparison.Ordinal))
                .ToList();

            Assert.NotEmpty(domainTypes);

            var offenders = new List<string>();
            foreach (var domainType in domainTypes)
            {
                foreach (var usedType in TypesUsedBy(domainType))
                {
                    if (IsDataverseSdkType(usedType))
                    {
                        offenders.Add(domainType.FullName + " uses " + usedType.FullName);
                    }
                }
            }

            Assert.Empty(offenders);
        }

        private static IEnumerable<Type> TypesUsedBy(Type type)
        {
            var used = new List<Type>();

            if (type.BaseType != null)
            {
                used.Add(type.BaseType);
            }

            used.AddRange(type.GetInterfaces());

            foreach (var field in type.GetFields(AllDeclaredMembers))
            {
                used.Add(field.FieldType);
            }

            foreach (var property in type.GetProperties(AllDeclaredMembers))
            {
                used.Add(property.PropertyType);
            }

            foreach (var method in type.GetMethods(AllDeclaredMembers).Cast<MethodBase>()
                .Concat(type.GetConstructors(AllDeclaredMembers)))
            {
                used.AddRange(method.GetParameters().Select(parameter => parameter.ParameterType));

                var methodInfo = method as MethodInfo;
                if (methodInfo != null)
                {
                    used.Add(methodInfo.ReturnType);
                }

                var body = method.GetMethodBody();
                if (body != null)
                {
                    used.AddRange(body.LocalVariables.Select(local => local.LocalType));
                }
            }

            return used.SelectMany(usedType => Flatten(usedType));
        }

        // Expands arrays and generic types (List<X>, X[]) so a wrapped SDK type is still found.
        private static IEnumerable<Type> Flatten(Type type)
        {
            yield return type;

            if (type.HasElementType)
            {
                foreach (var inner in Flatten(type.GetElementType()))
                {
                    yield return inner;
                }
            }

            if (type.IsGenericType)
            {
                foreach (var argument in type.GetGenericArguments())
                {
                    foreach (var inner in Flatten(argument))
                    {
                        yield return inner;
                    }
                }
            }
        }

        private static bool IsDataverseSdkType(Type type)
        {
            var assemblyName = type.Assembly.GetName().Name;

            return assemblyName.StartsWith("Microsoft.Xrm", StringComparison.Ordinal)
                || assemblyName.StartsWith("Microsoft.Crm", StringComparison.Ordinal)
                || (type.Namespace != null && type.Namespace.StartsWith("Microsoft.Xrm", StringComparison.Ordinal));
        }
    }
}
