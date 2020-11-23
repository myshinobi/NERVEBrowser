using CefSharp.OffScreen;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NERVEBrowser
{
    public class HTMLElement
    {
        public string tag;
        public string id;
        public string attributeName;
        public string attributeValue;
        public int substringLength;
        public string[] innerText;
        public bool exists = false;
        public RectangleF rectangle;
        public Search SearchType { get; private set; }
        public PickType pick;
        //public int parentRecursives = 0;
        public string javascriptVariable;
        private string ChildId;
        private int ChildGenerationDistance;
        private string ParentId;
        public string XPath;
        public enum PickType
        {
            First = 0,
            Random = 1,
            RandomBigger = 2
        }
        public enum Search
        {
            Id = 0,
            TagAttributeValue = 1,
            TagInnerText = 2,
            Tag = 3,
            HasAttribute = 4,
            AttributeValue = 5,
            XPath = 6,
            ParentElement = 7
        }


        public bool SearchFromChild()
        {
            return !string.IsNullOrEmpty(ChildId);
        }

        public bool SearchFromParent()
        {
            return !string.IsNullOrEmpty(ParentId);
        }

        public HTMLElement()
        {

        }

        public HTMLElement(string id)
        {
            this.id = id;
            UpdateSearchType();
        }

        public HTMLElement(string tag, string attributeName, string attributeValue, int substringLength = 0)
        {
            this.tag = tag;
            this.attributeName = attributeName;
            this.attributeValue = attributeValue;
            this.substringLength = substringLength;
            UpdateSearchType();
        }

        public HTMLElement(string tag,params string[] innerText)
        {
            this.tag = tag;
            this.innerText = innerText;
            UpdateSearchType();
        }

        //public void SetBoundingToParent(int recursives)
        //{
        //    parentRecursives = recursives;
        //}
        public float ReadValue(object value)
        {
            float final;
            if (float.TryParse(value.ToString(), out final))
            {
                return final;
            }



            return (float)value;
        }
        private RectangleF ReadRectangle(IDictionary<String, Object> rect)
        {

            RectangleF rectf = new RectangleF(ReadValue(rect["x"]), ReadValue(rect["y"]), ReadValue(rect["width"]), ReadValue(rect["height"]));
            return rectf;
        }

        //public async Task FindJavascriptElement(Headless chrome)
        //{
        //    string javascript = GetElementJavascript();
        //    object data = await chrome.GetJavaScriptResult(javascript);
        //    if (data.ToString() == "null")
        //    {
        //        exists = false;
        //    }
        //    else
        //    {
        //        exists = true;
        //        javascriptVariable = data.ToString();
        //    }
        //}

        public async Task<string> GetSrcAsync(Headless chrome, bool pickNew = false)
        {
            try
            {
                string script = GetMyJavascript(GetMyVariableName() + ".src", pickNew);
                object data = await chrome.GetJavaScriptResult(script);
                if (data.ToString() == "null")
                {
                    exists = false;
                }
                else
                {
                    string result = (string)data;
                    exists = true;
                    return result;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                exists = false;
            }

            return "";
        }

        
        public async Task<T> GetMyVariableData<T>(string VarOrFunctionName, Headless chrome, bool pickNew = false)
        {
            try
            {
                string script = GetMyJavascript(GetMyVariableName() + "."+ VarOrFunctionName, pickNew);
                object data = await chrome.GetJavaScriptResult(script);
                if (data.ToString() == "null")
                {
                    exists = false;
                }
                else
                {
                    T result;
                    try
                    {
                        result = (T)data;
                    }
                    catch (Exception)
                    {

                        result = Newtonsoft.Json.JsonConvert.DeserializeObject<T>((string)data);
                        throw;
                    }

                    exists = true;
                    return result;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                exists = false;
            }

            return default(T);
        }
        public async Task<string> GetInnerTextAsync(Headless chrome, bool pickNew = false)
        {
            return await GetMyVariableData<string>("innerText", chrome, pickNew);
        }

        public async Task UpdateRectangleAsync(Headless chrome, bool pickNew = false)
        {
            try
            {
                string script = GetBoundingJavascript(pickNew);
                object data = await chrome.GetJavaScriptResult(script);
                if (data.ToString() == "null")
                {
                    exists = false;
                }
                else
                {
                    IDictionary<String, Object> result = (IDictionary<String, Object>)data;
                    rectangle = ReadRectangle(result);
                    if (rectangle.Width*rectangle.Height>0)
                    {
                        exists = true;
                    }
                    else
                    {
                        exists = false;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                exists = false;
            }
            
        }

        public HTMLElement GetChildHTMLElement(HTMLElement childElement)
        {
            if (!this.exists)
            {
                throw new Exception(this.ToString() + " hasn't been located yet");
            }

            childElement.ParentId = GetMyVariableName();

            return childElement;
        }

        public HTMLElement GetParentHTMLElement(int generationDistance)
        {
            if (!this.exists)
            {
                throw new Exception(this.ToString() + " hasn't been located yet");
            }

            HTMLElement parentElement = new HTMLElement();

            parentElement.ChildId = GetMyVariableName();
            parentElement.ChildGenerationDistance = generationDistance;

            return parentElement;
        }

        public HTMLElement GetParentHTMLElement(HTMLElement parentElement)
        {
            if (!this.exists)
            {
                throw new Exception(this.ToString() + " hasn't been located yet");
            }

            parentElement.ChildId = GetMyVariableName();

            return parentElement;
        }

        private bool StringArrayIsEmpty(string[] strings)
        {
            if (strings == null)
            {
                return true;
            }

            if (strings.Count() <= 0)
            {
                return true;
            }

            return false;
        }

        private void UpdateSearchType()
        {
            if (ChildGenerationDistance > 0)
            {
                SearchType = Search.ParentElement;
            }


            if (string.IsNullOrEmpty(XPath)==false)
            {
                SearchType = Search.XPath;
                return;
            }
            if (string.IsNullOrEmpty(id) == false)
            {
                SearchType = Search.Id;
                return;
            }

            if (string.IsNullOrEmpty(tag) == false)
            {
                SearchType = Search.Tag;
                if (StringArrayIsEmpty(innerText) == false)
                {
                    SearchType = Search.TagInnerText;
                }

                if (string.IsNullOrEmpty(attributeName)==false)
                {
                    SearchType = Search.TagAttributeValue;
                }

            }
            else
            {

                if (string.IsNullOrEmpty(attributeName) == false)
                {
                    if (string.IsNullOrEmpty(attributeValue) == false)
                    {
                        SearchType = Search.AttributeValue;
                    }
                    else
                    {
                        SearchType = Search.HasAttribute;
                    }
                }
            }
        }

        private string GetSubstring()
        {
            return (substringLength > 0 ? ".substr(0,"+substringLength.ToString()+")": "");
        }

        private string GetCreatePicker()
        {
            return (pick != PickType.First ? (!exists ? @"var _lastElementIndex = 0; " : "")+"var _elementPickerItems = new Array();" : "");
        }

        private string RecursiveString(string str, int count)
        {
            string final = "";
            int loops = 0;
            while(loops < count)
            {
                final += str;
                loops++;
            }

            return final;
        }

        private string GetPickAddItem()
        {

            switch (pick)
            {
                case PickType.First:
                    return @"_lastElementIndex = i; "+ GetMyVariableName() + @" = _elements[i];  break;";

                case PickType.Random:

                    return @"_elementPickerItems.push(_elements[i]);";
                case PickType.RandomBigger:

                    return @"if (i > _lastElementIndex-40) {_lastElementIndex = i;  _elementPickerItems.push(_elements[i]); }";

                default:
                    break;
            }

            return "";
        }

        private string GetSetPickItem()
        {
            return GetMyVariableName() + @" = _elementPickerItems[Math.floor(Math.random()*_elementPickerItems.length)];" ;
        }

        private string GetFirstItem()
        {
            return GetMyVariableName() + @" = _elementPickerItems[0];";
        }

        private string GetMyVariableName()//bool addRecursiveParents = false)
        {
            return @"_element_" + this.GetHashCode().ToString();// +(addRecursiveParents ? RecursiveString(".parentElement", parentRecursives) : "").ToString();
        }

        public string GetMyJavascript(string scriptIfElementExists,bool pickNew)
        {

            if (SearchFromChild())
            {
                return GetParentFromChildElementJavascript(scriptIfElementExists, pickNew);
            }

            if (SearchFromParent())
            {
                return GetChildFromParentElementJavascript(scriptIfElementExists, pickNew);
            }

            return GetElementJavascript(scriptIfElementExists, pickNew);
        }

        public string GetBoundingJavascript(bool pickNew)
        {
            return GetMyJavascript(GetMyVariableName(
                ) +  ".getBoundingClientRect();", pickNew);
        }

        public string GetChildFromParentElementJavascript(string scriptIfElementExists = "", bool pickNew = false)
        {
            return GetElementJavascript(scriptIfElementExists, pickNew, ParentId);
        }

        public string GetParentLoop(string compare)
        {
            return @"_done = false;
_element = " + ChildId + @";
while(!_done)
{
	_element = _element.parentElement;
    if (_element==null)
    {
    	done = true;
    	break;
    }	
    else
    {
    	if ("+compare+@")
        {
            " + GetMyVariableName() + @" = _element;
            done = true;
            break;
        }
    }

}";
        }

        private string GetJavascriptElement(string script)
        {
            return @"if (typeof " + GetMyVariableName() + @" !== 'undefined' && " + GetMyVariableName() + " !== null) { " + GetMyVariableName() + @"; } else { " + script + @" }";
        }

        public string GetParentFromChildElementJavascript(string scriptIfElementExists = "", bool pickNew = false)
        {
            UpdateSearchType();
            string script = (pickNew ? GetMyVariableName() + " = undefined;" : "");
            switch (SearchType)
            {
                case Search.Id:
                    throw new Exception("selecing parent by id is not implemented yet");

                case Search.TagAttributeValue:
                    throw new Exception("selecing parent by tag and attribute is not implemented yet");

                case Search.TagInnerText:
                    throw new Exception("selecing parent by tag and inner text is not implemented yet");

                case Search.Tag:
                    script += GetJavascriptElement(GetParentLoop(@"_element.tagName.toLowerCase() == '" + tag.ToLower() + @"'"));
                    break;

                case Search.HasAttribute:

                    script += GetJavascriptElement(GetParentLoop(@"_element.hasAttribute('" + attributeName.ToLower() + @"')"));
                    break;

                case Search.ParentElement:
                    script += GetJavascriptElement(GetMyVariableName()+" = "+ ChildId + RecursiveString(@".parentElement", ChildGenerationDistance));

                    break;

                default:
                    throw new Exception("selecing parent by "+ SearchType.ToString()+ @" is not implemented yet");

            }


            return script + @"

if (typeof " + GetMyVariableName() + @" !== 'undefined' && " + GetMyVariableName() + " !== null) {" + '"' + GetMyVariableName() + '"' + @"; " + scriptIfElementExists + @"}
";
        }

        public string EvaluateExpression(string elementName, string expression)
        {
            Regex regex = new Regex(@"\{([^\}]+)\}");

            return regex.Replace(expression, "'+"+elementName + ".$1+'");
        }

        public string GetElementJavascript(string scriptIfElementExists = "", bool pickNew = false, string parent = "document")
        {

            UpdateSearchType();
            string script = (pickNew ? GetMyVariableName()+" = undefined;": "");
            script += @"
if (typeof " + GetMyVariableName() + @" !== 'undefined' && " + GetMyVariableName() + " !== null) { " + GetMyVariableName() + @"; } else { ";
            switch (SearchType)
            {
                case Search.Id:
                    script += GetMyVariableName() + @" = document.getElementById('" + id + @"');";
                    break;

                case Search.TagAttributeValue:
                    script += @"var _elements = " + parent + @".getElementsByTagName('" + tag + @"');
" + GetCreatePicker() + @"
for (var i = 0; i < _elements.length; i++) {
    if (_elements[i].getAttribute('" + attributeName + @"')" + GetSubstring() + @" == '" + EvaluateExpression(@"_elements[i]", attributeValue) + @"') {" + @"
        " + GetPickAddItem() + @"
    }
}

" + (pick != PickType.First ? GetSetPickItem() : "");

                    break;

                case Search.TagInnerText:
                    string compare = string.Join(@" || ", innerText.Select(x => @"_innerText == '" + x + @"'"));
                    script += @"var _elements = " + parent + @".getElementsByTagName('" + tag + @"');
" + GetCreatePicker() + @"
for (var i = 0; i < _elements.length; i++) {
var _innerText = _elements[i].innerText" + GetSubstring()+@";
    if ("+ compare + @") {
        "+ GetMyVariableName() + @" = _elements[i];
        break;
    }
}";


                    break;

                case Search.Tag:
                    script += @"var _elementPickerItems = " + parent + @".getElementsByTagName('" + tag + @"');

" + (pick != PickType.First ? GetSetPickItem() : GetFirstItem());
                    break;

                case Search.XPath:
                    script += GetMyVariableName() + @" = document.evaluate('"+XPath.Replace('\'','"')+"',document,null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue;";
                    break;

                default:
                    throw new Exception("selecing by " + SearchType.ToString() + @" is not implemented yet");
            }
            script += @"}";
            return script +@"

if (typeof "+GetMyVariableName()+@" !== 'undefined' && "+ GetMyVariableName() + " !== null) {"+ '"' + GetMyVariableName() + '"'+@"; "+ scriptIfElementExists + @"}
";
        }

        public override string ToString()
        {
            switch (SearchType)
            {
                case Search.Id:
                    return id;

                case Search.TagAttributeValue:
                    return tag+"."+attributeName+"="+attributeValue;

                case Search.TagInnerText:
                    return tag + ".innerText=" + string.Join(@" or ",innerText);

                case Search.AttributeValue:
                    return attributeName + "=" + attributeValue;

                case Search.HasAttribute:
                    return "hasAttribute: "+attributeName;

                case Search.XPath:
                    return "XPath: "+XPath;

                case Search.Tag:
                    return tag;

                case Search.ParentElement:
                    return ChildId+ RecursiveString(@".parentElement", ChildGenerationDistance);

                default:
                    return base.ToString();

            }
        }
    }
}
