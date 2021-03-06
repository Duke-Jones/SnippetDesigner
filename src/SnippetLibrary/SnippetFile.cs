using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Schema;
using Microsoft.RegistryTools;

namespace Microsoft.SnippetLibrary
{
    public class SnippetFile
    {
        private XmlSchemaSet schemas;
        private XmlNamespaceManager nsMgr;
        public static readonly string SnippetSchemaFormat = RegistryLocations.GetVSInstallDir() + @"..\..\Xml\Schemas\{0}\snippetformat.xsd";
        public static readonly string SnippetNS = @"http://schemas.microsoft.com/VisualStudio/2005/CodeSnippet";
        private bool validateSnippetFile;

        public XmlDocument SnippetXmlDoc { get; private set; }

        public bool HasXmlErrors { get; private set; }

        public string FileName { get; private set; }

        public List<Snippet> Snippets { get; private set; }

        public void CreateBlankSnippet()
        {
            LoadSchema();
            InitializeNewDocument();
        }

        public SnippetFile(string fileName)
        {
            Snippets = new List<Snippet>();
            FileName = fileName;
            LoadSchema();
            LoadData();
        }

        private void LoadSchema()
        {
            schemas = new XmlSchemaSet();
            if (validateSnippetFile)
            {
                var schemaPath = GetSnippetSchemaPath();
                if (!string.IsNullOrEmpty(schemaPath))
                {
                    schemas.Add(SnippetNS, schemaPath);
                }
            }
        }

        private static string GetSnippetSchemaPath()
        {
            int uiLanguage = RegistryLocations.GetVSUILanguage();
            string snippetSchema = string.Format(SnippetSchemaFormat, uiLanguage);
            if (File.Exists(snippetSchema)) return snippetSchema;

            snippetSchema = string.Format(SnippetSchemaFormat, CultureInfo.CurrentCulture.LCID);
            if (File.Exists(snippetSchema)) return snippetSchema;

            snippetSchema = string.Format(SnippetSchemaFormat, 1033);
            if (File.Exists(snippetSchema)) return snippetSchema;

            return null;
        }


        public void CreateFromText(string text)
        {
            LoadSchema();
            SnippetXmlDoc = new XmlDocument();

            if (schemas.Count > 0)
            {
                SnippetXmlDoc.Schemas = schemas;
            }
            SnippetXmlDoc.LoadXml(text);
            LoadFromDoc();
        }


        public void CreateSnippetFileFromNode(XmlNode snippetNode)
        {
            SnippetXmlDoc = new XmlDocument();
            SnippetXmlDoc.Schemas = schemas;
            SnippetXmlDoc.LoadXml("<?xml version=\"1.0\" encoding=\"utf-8\" ?>" +
                                  "<CodeSnippets xmlns=\"" + SnippetNS + "\">" +
                                  snippetNode.OuterXml
                                  + "</CodeSnippets>");

            Snippets = new List<Snippet>();
            nsMgr = new XmlNamespaceManager(SnippetXmlDoc.NameTable);
            nsMgr.AddNamespace("ns1", SnippetNS);

            XmlNode node = SnippetXmlDoc.SelectSingleNode("//ns1:CodeSnippets//ns1:CodeSnippet", nsMgr);
            Snippets.Add(new Snippet(node, nsMgr));
        }


        public void Save()
        {
            SnippetXmlDoc.Save(FileName);
        }

        public void SaveAs(string fileName)
        {
            SnippetXmlDoc.Save(fileName);
            FileName = fileName;
        }

        private void SchemaValidationEventHandler(object sender, ValidationEventArgs e)
        {
            switch (e.Severity)
            {
                case XmlSeverityType.Error:
                    {
                        HasXmlErrors = true;
                        if (validateSnippetFile)
                        {
                            MessageBox.Show(String.Format("\nError: {0}", e.Message));
                        }
                        break;
                    }
                case XmlSeverityType.Warning:
                    {
                        HasXmlErrors = true;
                        if (validateSnippetFile)
                        {
                            MessageBox.Show(String.Format("\nWarning: {0}", e.Message));
                        }
                        break;
                    }
            }
        }


        /// <summary>
        /// Appends the new snippet.
        /// </summary>
        /// <returns>Index position of added snippet</returns>
        public int AppendNewSnippet()
        {
            XmlElement newSnippet = SnippetXmlDoc.CreateElement("CodeSnippet", nsMgr.LookupNamespace("ns1"));
            newSnippet.SetAttribute("Format", "1.0.0");
            newSnippet.AppendChild(SnippetXmlDoc.CreateElement("Header", nsMgr.LookupNamespace("ns1")));
            newSnippet.AppendChild(SnippetXmlDoc.CreateElement("Snippet", nsMgr.LookupNamespace("ns1")));

            XmlNode codeSnippetsNode = SnippetXmlDoc.SelectSingleNode("//ns1:CodeSnippets", nsMgr);
            XmlNode newNode = codeSnippetsNode.AppendChild(newSnippet);
            Snippets.Add(new Snippet(newNode, nsMgr));
            return Snippets.Count - 1;
        }


        /// <summary>
        /// Initializes the new document.
        /// </summary>
        private void InitializeNewDocument()
        {
            SnippetXmlDoc = new XmlDocument();
            SnippetXmlDoc.Schemas = schemas;
            SnippetXmlDoc.LoadXml("<?xml version=\"1.0\" encoding=\"utf-8\" ?>" +
                                  "<CodeSnippets xmlns=\"" + SnippetNS + "\">" +
                                  "<CodeSnippet Format=\"1.0.0\"><Header></Header>" +
                                  "<Snippet>" +
                                  "</Snippet></CodeSnippet></CodeSnippets>");

            Snippets = new List<Snippet>();
            nsMgr = new XmlNamespaceManager(SnippetXmlDoc.NameTable);
            nsMgr.AddNamespace("ns1", SnippetNS);

            XmlNode node = SnippetXmlDoc.SelectSingleNode("//ns1:CodeSnippets//ns1:CodeSnippet", nsMgr);
            Snippets.Add(new Snippet(node, nsMgr));
        }

        private void LoadFromDoc()
        {
            nsMgr = new XmlNamespaceManager(SnippetXmlDoc.NameTable);
            nsMgr.AddNamespace("ns1", "http://schemas.microsoft.com/VisualStudio/2005/CodeSnippet");


            // If the document doesn't already have a declaration, add it
            if (SnippetXmlDoc.FirstChild.NodeType != XmlNodeType.XmlDeclaration)
            {
                XmlDeclaration decl = SnippetXmlDoc.CreateXmlDeclaration("1.0", "utf-8", string.Empty);
                SnippetXmlDoc.InsertBefore(decl, SnippetXmlDoc.DocumentElement);
            }

            // Handle the current ambiguity as to whether
            // the root node "CodeSnippets" is optional or not
            if (SnippetXmlDoc.DocumentElement.Name == "CodeSnippet")
            {
                // Since the root element was CodeSnippet, we should
                // proceed with the assumption that this file only
                // defines one snippet.
                Snippets.Add(new Snippet(SnippetXmlDoc.DocumentElement, nsMgr));
                return;
            }
            else
            {
                foreach (XmlNode node in SnippetXmlDoc.DocumentElement.SelectNodes("//ns1:CodeSnippet", nsMgr))
                {
                    Snippets.Add(new Snippet(node, nsMgr));
                }
            }
            if (schemas.Count > 0)
            {
                SnippetXmlDoc.Schemas = schemas;
                ValidationEventHandler schemaValidator = SchemaValidationEventHandler;
                SnippetXmlDoc.Validate(schemaValidator);
            }
        }

        // Read in the xml document and extract relevant data
        private void LoadData()
        {
            SnippetXmlDoc = new XmlDocument();

            try
            {
                if (!String.IsNullOrEmpty(FileName))
                {
                    //if file name exists use it otherweise use stream if it exists
                    SnippetXmlDoc.Load(FileName);
                }
                else
                {
                    throw new IOException("No data to read from");
                }
            }
            catch (IOException ioException)
            {
                //if file doesnt exist or cant be read throw the ioexcpetion
                throw ioException;
            }
            catch (XmlException)
            {
                //check if this file is empty if so then initialize a new file
                if (!String.IsNullOrEmpty(FileName) && File.ReadAllText(FileName).Trim() == String.Empty)
                {
                    InitializeNewDocument();
                }
                else
                {
                    //the file is not empty
                    //we shouldnt be loading this
                    throw new IOException("Not a valid XML Document");
                }
                return;
            }
            //load from the stored XMLdocument
            LoadFromDoc();
        }
    }
}