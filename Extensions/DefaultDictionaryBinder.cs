using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.ComponentModel;
using System.Collections;

    /// <summary>
    /// ASP.NET MVC Default Dictionary Binder
    /// </summary>
    public class DefaultDictionaryBinder : DefaultModelBinder
    {
        IModelBinder nextBinder;

        /// <summary>
        /// Create an instance of DefaultDictionaryBinder.
        /// </summary>
        public DefaultDictionaryBinder() : this(null)
        {
        }

        /// <summary>
        /// Create an instance of DefaultDictionaryBinder.
        /// </summary>
        /// <param name="nextBinder">The next model binder to chain call. If null, by default, the DefaultModelBinder is called.</param>
        public DefaultDictionaryBinder(IModelBinder nextBinder)
        {
            this.nextBinder = nextBinder;
        }

        private IEnumerable<string> GetValueProviderKeys(ControllerContext context)
        {
#if !ASPNETMVC1
            List<string> keys = new List<string>();
            keys.AddRange(context.HttpContext.Request.Form.Keys.Cast<string>());
            keys.AddRange(((IDictionary<string, object>)context.RouteData.Values).Keys.Cast<string>());
            keys.AddRange(context.HttpContext.Request.QueryString.Keys.Cast<string>());
            keys.AddRange(context.HttpContext.Request.Files.Keys.Cast<string>());
            return keys;
#else
            return bindingContext.ValueProvider.Keys;
#endif
        }

        private object ConvertType(string stringValue, Type type)
        {
            return TypeDescriptor.GetConverter(type).ConvertFrom(stringValue);
        }

        public override object BindModel(ControllerContext controllerContext, ModelBindingContext bindingContext)
        {
            Type modelType = bindingContext.ModelType;
            Type idictType = null;
            if (modelType.IsInterface && modelType.IsGenericType && modelType.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            {
                idictType = modelType;
            }
            if (idictType != null)
            {
                IDictionary result = null;

                Type[] ga = idictType.GetGenericArguments();
                IModelBinder valueBinder = Binders.GetBinder(ga[1]);

                foreach (string key in GetValueProviderKeys(controllerContext))
                {
                    int startbracket = key.StartsWith(bindingContext.ModelName + "[", StringComparison.InvariantCultureIgnoreCase)
                        ? bindingContext.ModelName.Length : (bindingContext.FallbackToEmptyPrefix ? key.IndexOf('[') : -1);

                    if (startbracket >= 0)
                    {
                        int endbracket = key.IndexOf("]", startbracket + 1);
                        if (endbracket == -1)
                            continue;

                        object dictKey;
                        try
                        {
                            dictKey = ConvertType(key.Substring(startbracket + 1, endbracket - startbracket - 1), ga[0]);
                        }
                        catch (NotSupportedException)
                        {
                            continue;
                        }

                        ModelBindingContext innerBindingContext = new ModelBindingContext()
                        {
#if ASPNETMVC1
                            Model = null,
                            ModelType = ga[1],
#else
                            ModelMetadata = ModelMetadataProviders.Current.GetMetadataForType(() => null, ga[1]),
#endif
                            ModelName = key.Substring(0, endbracket + 1),
                            ModelState = bindingContext.ModelState,
                            PropertyFilter = bindingContext.PropertyFilter,
                            ValueProvider = bindingContext.ValueProvider
                        };
                        object newPropertyValue = valueBinder.BindModel(controllerContext, innerBindingContext);

                        if (result == null)
                            result = (IDictionary)CreateModel(controllerContext, bindingContext, modelType);

                        if (!result.Contains(dictKey))
                        {
                            result.Add(dictKey, newPropertyValue);
                        }
                    }
                }

                return result;
            }

            if (nextBinder != null)
            {
                return nextBinder.BindModel(controllerContext, bindingContext);
            }

            return base.BindModel(controllerContext, bindingContext);
        }
    }

