using Liquid.Domain;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Liquid.Activation
{
    internal class InputValidator
    {
        private readonly Dictionary<string, object[]> errors = [];
        internal Dictionary<string, object[]> Errors => errors;
        internal int ErrorsCount => errors.Count;

        internal void AddInputError(string message)
        {
            if (!errors.ContainsKey(message))
                errors.TryAdd(message, null);
        }

        internal void AddInputValidationErrorCode(string error)
        {
            if (!errors.ContainsKey(error))
                errors.TryAdd(error, null);
        }

        internal void AddInputValidationErrorCode(string error, params object[] args)
        {
            if (!errors.ContainsKey(error))
                errors.TryAdd(error, args);
        }

        /// <summary>
        /// The method receives the ViewModel to input validation and add on errors list.
        /// (if there are errors after validation ViewModel.)
        /// </summary>
        /// <param name="viewModel">The ViewModel to input validation</param>
        internal void ValidateInput(dynamic viewModel)
        {
            if (viewModel is null)
            {
                AddInputError("paremeters malformed or empty");
                return;
            }

            viewModel.InputErrors = errors;
            viewModel.ValidateModel();
            ResultValidation result = viewModel.ModelValidator.Validate(viewModel);
            if (!result.IsValid)
                foreach (var error in result.Errors)
                    // Adds an input validation error.
                    AddInputValidationErrorCode(error.Key, error.Value);

            //By reflection, browse viewModel by identifying all property attributes and lists for validation.  
            foreach (PropertyInfo propInfo in viewModel.GetType().GetProperties())
            {
                dynamic child = propInfo.GetValue(viewModel);

                //When the child is a list, validate each of its members  
                if (child is IList)
                {
                    var children = (IList)propInfo.GetValue(viewModel);
                    foreach (var item in children)
                        //Check, if the property is a Light ViewModel, only they will validation Lights ViewModel
                        if (item is not null
                             && (item.GetType().BaseType != typeof(object))
                             && (item.GetType().BaseType != typeof(System.ValueType))
                             && (item.GetType().BaseType.IsGenericType
                                  && (item.GetType().BaseType.Name.StartsWith("LightViewModel")
                                       || item.GetType().BaseType.Name.StartsWith("LightValueObject"))))
                        {
                            dynamic obj = item;
                            //Check, if the attribute is null for verification of the type.
                            if (obj is not null)
                                ValidateInput(obj);
                        }
                }
                //Otherwise, validate the very child once. 
                else if (child is not null)
                    //Check, if the property is a Light ViewModel, only they will validation Lights ViewModel
                    if ((child.GetType().BaseType != typeof(object))
                         && (child.GetType().BaseType != typeof(System.ValueType))
                         && (child.GetType().BaseType.IsGenericType
                              && (child.GetType().BaseType.Name.StartsWith("LightViewModel")
                                   || child.GetType().BaseType.Name.StartsWith("LightValueObject"))))
                        ValidateInput(child);
            }

            //By reflection, browse viewModel by identifying all field attributes and lists for validation.  
            foreach (FieldInfo fieldInfo in viewModel.GetType().GetFields())
            {
                dynamic child = fieldInfo.GetValue(viewModel);

                //When the child is a list, validate each of its members  
                if (child is IList)
                {
                    var children = (IList)fieldInfo.GetValue(viewModel);
                    foreach (var item in children)
                        //Check, if the property is a Light ViewModel, only they will validation Lights ViewModel
                        if (item is not null
                             && (item.GetType().BaseType != typeof(object))
                             && (item.GetType().BaseType != typeof(System.ValueType))
                             && (item.GetType().BaseType.IsGenericType
                                  && (item.GetType().BaseType.Name.StartsWith("LightViewModel")
                                       || item.GetType().BaseType.Name.StartsWith("LightValueObject"))))
                        {
                            dynamic obj = item;
                            //Check, if the attribute is null for verification of the type.
                            if (obj is not null)
                                ValidateInput(obj);
                        }
                }
                //Otherwise, validate the very child once. 
                else if (child is not null)
                {

                    //Check, if the property is a Light ViewModel, only they will validation Lights ViewModel
                    if ((child.GetType().BaseType != typeof(object))
                         && (child.GetType().BaseType != typeof(System.ValueType))
                         && (child.GetType().BaseType.IsGenericType
                              && (child.GetType().BaseType.Name.StartsWith("LightViewModel")
                                   || child.GetType().BaseType.Name.StartsWith("LightValueObject"))))
                        ValidateInput(child);
                }
            }
        }
    }
}