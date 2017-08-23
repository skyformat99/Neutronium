﻿using MoreCollection.Extensions;
using Neutronium.Core.Binding.Builder.Packer;
using Neutronium.Core.Binding.GlueObject;
using Neutronium.Core.WebBrowserEngine.JavascriptObject;
using System;
using System.Collections.Generic;
using System.Linq;


namespace Neutronium.Core.Binding.Builder
{
    internal class JavascriptObjectBulkBuilderStrategy : IJavascriptObjectBuilderStrategy, IBulkUpdater
    {
        public IJavascriptObject CommandConstructor => _Factory.Value.CommandConstructor;
        public IJavascriptObject ExecutableConstructor => _Factory.Value.ExecutableConstructor;

        private readonly IWebView _WebView;
        private readonly IJavascriptSessionCache _Cache;
        private readonly Lazy<BulkJsHelper> _Factory;
        private readonly bool _Mapping;
        private IJavascriptObject _BulkCreator => _Factory.Value.BulkCreator;
        
        public JavascriptObjectBulkBuilderStrategy(IWebView webView, IJavascriptSessionCache cache, bool mapping)
        {
            _Mapping = mapping;
            _WebView = webView;
            _Cache = cache;
            _Factory = new Lazy<BulkJsHelper>(FactoryBuilder);
        }

        public void UpdateJavascriptValue(IJSCSGlue root)
        {
            var builder = new JavascriptObjectBulkBuilder(_WebView.Factory, _Cache, this, root, _Mapping);
            builder.UpdateJavascriptValue();
        }

        private BulkJsHelper FactoryBuilder()
        {
            var script =
                 @"(function(){
                    function Command(id, canExecute){
                        Object.defineProperty(this, '{{NeutroniumConstants.ObjectId}}', {value: id});
                        Object.defineProperty(this, '{{NeutroniumConstants.ReadOnlyFlag}}', {value: true});
                        this.CanExecuteCount = 1;
                        this.CanExecuteValue = canExecute;
                    }
                    Command.prototype.Execute = function() {
                        this.privateExecute(this.{{NeutroniumConstants.ObjectId}}, ...arguments)
                    }
                    Command.prototype.CanExecute = function() {
                        this.privateCanExecute(this.{{NeutroniumConstants.ObjectId}}, ...arguments)
                    }
                    function Executable(id){
                        Object.defineProperty(this, '{{NeutroniumConstants.ObjectId}}', {value: id});
                        Object.defineProperty(this, '{{NeutroniumConstants.ReadOnlyFlag}}', {value: true});
                    }
                    Executable.prototype.Execute = function() {
                        this.privateExecute(this.{{NeutroniumConstants.ObjectId}}, ...arguments)
                    }
                    function bulkCreate(prop){
                        const propss = JSON.parse(prop)
                        const count = propss.count
		                const args = Array.from(arguments)
		                const objs = args.slice(1, count + 1)
		                const values = args.slice(1 + count, args.length + 1)
                        var valueCount = 0
                        var elementCount = 0
                        var innerCount = 0
                        const elements = propss.elements
                        var element = null
		                for(var i=0; i< count; i ++){
                            if (!element || innerCount > element.c) {
                                element = elements[elementCount++]
                                innerCount = 0;
                            }
                            var props = element.a
                            for (var j = 0, len = props.length; j < len; j++) {
                                objs[i][props[j]] = values[valueCount++]
                            }
                            innerCount++
		                }
                    }
                    return {
                        bulkCreate,
                        Command,
                        Executable
                    }
                })()";

            IJavascriptObject helper;
            script = script.Replace("{{NeutroniumConstants.ObjectId}}", NeutroniumConstants.ObjectId)
                            .Replace("{{NeutroniumConstants.ReadOnlyFlag}}", NeutroniumConstants.ReadOnlyFlag);
            _WebView.Eval(script, out helper);
            return new BulkJsHelper(_Cache, _WebView, helper);
        }

        void IBulkUpdater.BulkUpdateProperty(IEnumerable<EntityDescriptor<string>> updates)
        {
            var orderedUpdates = updates.GroupBy(up => up.Father.CValue.GetType()).SelectMany(grouped => grouped);
            BulkUpdate(orderedUpdates, new ObjectChildrenDescriptionPacker());
        }

        void IBulkUpdater.BulkUpdateArray(IEnumerable<EntityDescriptor<int>> updates)
        {
            BulkUpdate(updates, new ArrayChildrenDescriptionPacker());
        }

        private void BulkUpdate<T>(IEnumerable<EntityDescriptor<T>> updates, IEntityDescriptorChildrenDescriptionPacker<T> packer)
        {
            var spliter = new EntityDescriptorSpliter<T> { MaxCount = _WebView.MaxFunctionArgumentsNumber -1 };

            foreach(var entityDescriptor in spliter.SplitParameters(updates))
            {
                var arguments = GetUpdateParameters(entityDescriptor, packer);
                Execute(arguments);
            }
        }

        private IJavascriptObject[] GetUpdateParameters<T>(List<EntityDescriptor<T>> updates, IEntityDescriptorChildrenDescriptionPacker<T> packer)
        {
            var sizes = packer.Pack(updates);
            var objects = updates.Select(up => up.Father);
            var values = updates.SelectMany(up => up.ChildrenDescription).Select(desc => desc.Value);
            return BuildArguments(sizes, objects.Concat(values));
        }

        private IJavascriptObject[] BuildArguments(string paramString, IEnumerable<IJSCSGlue> paramsObjects)
        {
            return new[] { _WebView.Factory.CreateString(paramString) }.Concat(paramsObjects.Select(glue => glue.JSValue)).ToArray();
        }

        private static string AsArray(IEnumerable<string> value) => $"[{string.Join(",", value)}]";

        private void Execute(params IJavascriptObject[] arguments)
        {
            _BulkCreator.ExecuteFunctionNoResult(_WebView, null, arguments);
        }
    }
}
