﻿using System.Collections.Generic;
using Neutronium.Core.Binding.GlueObject;
using Neutronium.Core.JavascriptFramework;
using Neutronium.Core.WebBrowserEngine.JavascriptObject;
using MoreCollection.Extensions;
using System;

namespace Neutronium.Core.Binding
{
    internal class SessionCacher : IJavascriptSessionCache
    {
        private readonly IDictionary<object, IJsCsGlue> _FromCSharp = new Dictionary<object, IJsCsGlue>();
        private readonly IDictionary<uint, IJsCsGlue> _FromJavascript_Global = new Dictionary<uint, IJsCsGlue>();

        public void CacheFromCSharpValue(object key, IJsCsGlue value)
        {
            _FromCSharp.Add(key, value);
        }

        public void RemoveFromCSharpToJs(IJsCsGlue value)
        {
            var key = value.CValue;
            if (key == null)
                return;

            _FromCSharp.Remove(key);
        }

        public void RemoveFromJsToCSharp(IJsCsGlue value)
        {
            var id = value.JsId;
            if (id == 0)
                return;

            _FromJavascript_Global.Remove(id);
        }

        public void Cache(IJsCsGlue value)
        {
            var cashable = value as IJsCsCachableGlue;
            if (cashable != null)
                Cache(cashable);
            else
                _FromJavascript_Global.Add(value.JsValue.GetID(), value);
        }

        public void Cache(IJsCsCachableGlue cachableGlue)
        {
            var id = cachableGlue.CachableJsValue.GetID();
            if (id == 0)
                return;

            cachableGlue.SetJsId(id);
            _FromJavascript_Global[id] = cachableGlue;
        }

        private void CacheGlobal(IJavascriptObject jsobject, IJsCsMappedBridge ibo)
        {
            var id = jsobject.GetID();
            if (id == 0)
                return;

            ibo.SetJsId(id);
            _FromJavascript_Global[id] = ibo;
        }

        public IJsCsGlue GetCached(object key)
        {
            return _FromCSharp.GetOrDefault(key);
        }

        public IJsCsGlue GetCached(IJavascriptObject globalkey) 
        {
            var id = globalkey.GetID();
            return (id == 0) ? null : _FromJavascript_Global.GetOrDefault(id);
        }

        public IJsCsGlue GetCached(uint id)
        {
            return _FromJavascript_Global.GetOrDefault(id);
        }

        public IJavascriptObjectInternalMapper GetMapper(IJsCsMappedBridge root)
        {
            return new JavascriptMapper(root, Update, RegisterMapping, RegisterCollectionMapping);
        }

        internal void Update(IJsCsMappedBridge observableBridge, IJavascriptObject jsobject)
        {
            observableBridge.SetMappedJSValue(jsobject);
            CacheGlobal(jsobject, observableBridge);
        }

        internal void RegisterMapping(IJavascriptObject father, string att, IJavascriptObject child)
        {
            var global = GetCached(father);
            if (global is JSCommand)
                return;

            var jso = (JsGenericObject)global;
            Update(jso.GetAttribute(att) as IJsCsMappedBridge, child);
        }

        internal void RegisterCollectionMapping(IJavascriptObject jsFather, string att, int index, IJavascriptObject child)
        {
            var father = GetCached(jsFather);
            var jsos = (att == null) ? father : ((JsGenericObject)father).GetAttribute(att);

            Update(((JsArray)jsos).Items[index] as IJsCsMappedBridge, child);
        }
    }
}
