﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using Sdl.Community.XLIFF.Manager.Common;
using Sdl.Community.XLIFF.Manager.FileTypeSupport.SDLXLIFF;
using Sdl.Community.XLIFF.Manager.FileTypeSupport.XLIFF.Model;
using Sdl.Community.XLIFF.Manager.Interfaces;
using Sdl.Core.Globalization;
using Sdl.FileTypeSupport.Framework.NativeApi;

namespace Sdl.Community.XLIFF.Manager.FileTypeSupport.XLIFF.Readers
{
	public class Xliff12PolyglotReader : IXliffReader
	{
		private readonly SegmentBuilder _segmentBuilder;

		public Xliff12PolyglotReader(SegmentBuilder segmentBuilder)
		{
			_segmentBuilder = segmentBuilder;
		}

		public Xliff ReadXliff(string filePath)
		{
			var xliff = new Xliff();

			var xmlTextReader = new XmlTextReader(filePath);
			var xmlReaderSettings = new XmlReaderSettings { ValidationType = ValidationType.None };
			using (var xmlReader = XmlReader.Create(xmlTextReader, xmlReaderSettings))
			{
				var index = 0;
				while (xmlReader.Read())
				{
					switch (xmlReader.NodeType)
					{
						case XmlNodeType.Element:
							if (index == 0 && string.Compare(xmlReader.Name, "xliff", StringComparison.OrdinalIgnoreCase) == 0)
							{
								index++;
								while (xmlReader.MoveToNextAttribute())
								{
									if (string.Compare(xmlReader.Name, "version", StringComparison.OrdinalIgnoreCase) == 0)
									{
										xliff.Version = xmlReader.Value;
									}
									else if (string.Compare(xmlReader.Name, "sdl:support", StringComparison.OrdinalIgnoreCase) == 0)
									{
										var success = Enum.TryParse(xmlReader.Value, true, out Enumerators.XLIFFSupport support);
										xliff.Support = success ? support : Enumerators.XLIFFSupport.none;
									}
									else if (string.Compare(xmlReader.Name, "sdl:version", StringComparison.OrdinalIgnoreCase) == 0)
									{
										xliff.SpecificVersion = xmlReader.Value;
									}
								}
							}
							else if (string.Compare(xmlReader.Name, "sdl:doc-info", StringComparison.OrdinalIgnoreCase) == 0)
							{
								var xmlReaderSub = xmlReader.ReadSubtree();
								xliff.DocInfo = ReadDocInfo(xmlReaderSub);
								xmlReaderSub.Close();
							}
							else if (string.Compare(xmlReader.Name, "file", StringComparison.OrdinalIgnoreCase) == 0)
							{
								var xmlReaderSub = xmlReader.ReadSubtree();
								xliff.Files.Add(ReadFile(xmlReaderSub));
								xmlReaderSub.Close();
							}
							break;
					}
				}
			}

			return xliff;
		}

		private DocInfo ReadDocInfo(XmlReader xmlReader)
		{
			var docInfo = new DocInfo();

			var index = 0;
			while (xmlReader.Read())
			{
				switch (xmlReader.NodeType)
				{
					case XmlNodeType.Element:
						if (index == 0 && string.Compare(xmlReader.Name, "sdl:doc-info", StringComparison.OrdinalIgnoreCase) == 0)
						{
							index++;
							while (xmlReader.MoveToNextAttribute())
							{
								if (string.Compare(xmlReader.Name, "project-id", StringComparison.OrdinalIgnoreCase) == 0)
								{
									docInfo.ProjectId = xmlReader.Value;
								}
								else if (string.Compare(xmlReader.Name, "source", StringComparison.OrdinalIgnoreCase) == 0)
								{
									docInfo.Source = xmlReader.Value;
								}
								else if (string.Compare(xmlReader.Name, "source-language", StringComparison.OrdinalIgnoreCase) == 0)
								{
									docInfo.SourceLanguage = xmlReader.Value;
								}
								else if (string.Compare(xmlReader.Name, "target-language", StringComparison.OrdinalIgnoreCase) == 0)
								{
									docInfo.TargetLanguage = xmlReader.Value;
								}
								else if (string.Compare(xmlReader.Name, "created", StringComparison.OrdinalIgnoreCase) == 0)
								{
									var format = "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'";
									var success = DateTime.TryParseExact(xmlReader.Value, format,
										CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateValue);

									docInfo.Created = success ? dateValue : DateTime.MinValue;
								}
							}
						}
						break;
				}
			}

			return docInfo;
		}

		private File ReadFile(XmlReader xmlReader)
		{
			var file = new File();

			var index = 0;
			while (xmlReader.Read())
			{
				switch (xmlReader.NodeType)
				{
					case XmlNodeType.Element:
						if (index == 0 && string.Compare(xmlReader.Name, "file", StringComparison.OrdinalIgnoreCase) == 0)
						{
							index++;
							while (xmlReader.MoveToNextAttribute())
							{
								if (string.Compare(xmlReader.Name, "original", StringComparison.OrdinalIgnoreCase) == 0)
								{
									file.Original = xmlReader.Value;
								}
								else if (string.Compare(xmlReader.Name, "source-language", StringComparison.OrdinalIgnoreCase) == 0)
								{
									file.SourceLanguage = xmlReader.Value;
								}
								else if (string.Compare(xmlReader.Name, "target-language", StringComparison.OrdinalIgnoreCase) == 0)
								{
									file.TargetLanguage = xmlReader.Value;
								}
								else if (string.Compare(xmlReader.Name, "datatype", StringComparison.OrdinalIgnoreCase) == 0)
								{
									file.DataType = xmlReader.Value;
								}
							}
						}
						else if (string.Compare(xmlReader.Name, "body", StringComparison.OrdinalIgnoreCase) == 0)
						{
							var xmlReaderSub = xmlReader.ReadSubtree();
							file.Body = ReadBody(xmlReaderSub);
							xmlReaderSub.Close();
						}
						break;
				}
			}

			return file;
		}

		private Body ReadBody(XmlReader xmlReader)
		{
			var body = new Body();

			while (xmlReader.Read())
			{
				switch (xmlReader.NodeType)
				{
					case XmlNodeType.Element:
						if (string.Compare(xmlReader.Name, "group", StringComparison.OrdinalIgnoreCase) == 0)
						{
							var xmlReaderSub = xmlReader.ReadSubtree();
							body.TransUnits.Add(ReadGroup(xmlReaderSub));
							xmlReaderSub.Close();
						}
						break;
				}
			}

			return body;
		}

		private TransUnit ReadGroup(XmlReader xmlReader)
		{
			var transUnit = new TransUnit();
			var paragraphId = string.Empty;

			var index = 0;
			while (xmlReader.Read())
			{
				switch (xmlReader.NodeType)
				{
					case XmlNodeType.Element:
						if (index == 0 && string.Compare(xmlReader.Name, "group", StringComparison.OrdinalIgnoreCase) == 0)
						{
							index++;
							while (xmlReader.MoveToNextAttribute())
							{
								if (string.Compare(xmlReader.Name, "id", StringComparison.OrdinalIgnoreCase) == 0)
								{
									// for polyglot support, the paragraph id is represented by the group id
									paragraphId = xmlReader.Value;
								}
							}
						}
						else if (string.Compare(xmlReader.Name, "trans-unit", StringComparison.OrdinalIgnoreCase) == 0)
						{
							var xmlReaderSub = xmlReader.ReadSubtree();
							transUnit = ReadTransUnit(xmlReaderSub);
							transUnit.Id = paragraphId;
							xmlReaderSub.Close();
						}
						break;
				}
			}

			return transUnit;
		}

		private TransUnit ReadTransUnit(XmlReader xmlReader)
		{
			var transUnit = new TransUnit();
			var segmentPair = new SegmentPair(_segmentBuilder);

			var index = 0;
			while (xmlReader.Read())
			{
				switch (xmlReader.NodeType)
				{
					case XmlNodeType.Element:
						if (index == 0 && string.Compare(xmlReader.Name, "trans-unit", StringComparison.OrdinalIgnoreCase) == 0)
						{
							index++;
							while (xmlReader.MoveToNextAttribute())
							{
								if (string.Compare(xmlReader.Name, "id", StringComparison.OrdinalIgnoreCase) == 0)
								{
									// for polyglot support, the segment id is represented by the trans-unit id
									segmentPair.Id = xmlReader.Value;
								}
								if (string.Compare(xmlReader.Name, "sdl:conf", StringComparison.OrdinalIgnoreCase) == 0)
								{
									var success = Enum.TryParse<ConfirmationLevel>(xmlReader.Value, true, out var value);
									segmentPair.ConfirmationLevel = success ? value : ConfirmationLevel.Unspecified;
								}
								if (string.Compare(xmlReader.Name, "sdl:locked", StringComparison.OrdinalIgnoreCase) == 0)
								{
									var success = bool.TryParse(xmlReader.Value, out var result);
									segmentPair.IsLocked = success && result;
								}
								if (string.Compare(xmlReader.Name, "sdl:origin", StringComparison.OrdinalIgnoreCase) == 0)
								{
									segmentPair.TranslationOrigin.OriginType = xmlReader.Value;
								}
								if (string.Compare(xmlReader.Name, "sdl:origin-system", StringComparison.OrdinalIgnoreCase) == 0)
								{
									segmentPair.TranslationOrigin.OriginSystem = xmlReader.Value;
								}
								if (string.Compare(xmlReader.Name, "sdl:percent", StringComparison.OrdinalIgnoreCase) == 0)
								{
									var success = byte.TryParse(xmlReader.Value, out var result);
									segmentPair.TranslationOrigin.MatchPercent = success ? result : (byte)0;
								}
								if (string.Compare(xmlReader.Name, "sdl:struct-match", StringComparison.OrdinalIgnoreCase) == 0)
								{
									var success = bool.TryParse(xmlReader.Value, out var result);
									segmentPair.TranslationOrigin.IsStructureContextMatch = success && result;
								}
								if (string.Compare(xmlReader.Name, "sdl:text-match", StringComparison.OrdinalIgnoreCase) == 0)
								{
									var success = Enum.TryParse<TextContextMatchLevel>(xmlReader.Value, true, out var result);
									segmentPair.TranslationOrigin.TextContextMatchLevel = success ? result : TextContextMatchLevel.None;
								}
							}
						}
						else if (string.Compare(xmlReader.Name, "source", StringComparison.OrdinalIgnoreCase) == 0)
						{
							var xmlReaderSub = xmlReader.ReadSubtree();
							segmentPair.Source = ReadSource(xmlReaderSub);
							xmlReaderSub.Close();
						}
						else if (string.Compare(xmlReader.Name, "target", StringComparison.OrdinalIgnoreCase) == 0)
						{
							var xmlReaderSub = xmlReader.ReadSubtree();
							segmentPair.Target = ReadTarget(xmlReaderSub);
							xmlReaderSub.Close();
						}
						break;
				}
			}
			transUnit.SegmentPairs.Add(segmentPair);
			return transUnit;
		}

		private Source ReadSource(XmlReader xmlReader)
		{
			var source = new Source();

			while (xmlReader.Read())
			{
				switch (xmlReader.NodeType)
				{
					case XmlNodeType.Element:
						if (string.Compare(xmlReader.Name, "bpt", StringComparison.OrdinalIgnoreCase) == 0)
						{
							var xmlReaderSub = xmlReader.ReadSubtree();
							source.Elements.Add(ReadOpeningElementTagPair(xmlReaderSub));
							xmlReaderSub.Close();
						}
						else if (string.Compare(xmlReader.Name, "ept", StringComparison.OrdinalIgnoreCase) == 0)
						{
							var xmlReaderSub = xmlReader.ReadSubtree();
							source.Elements.Add(ReadClosingElementTagPair(xmlReaderSub));
							xmlReaderSub.Close();
						}
						else if (string.Compare(xmlReader.Name, "ph", StringComparison.OrdinalIgnoreCase) == 0)
						{
							var xmlReaderSub = xmlReader.ReadSubtree();
							source.Elements.Add(ReadElementPlaceholder(xmlReaderSub));
							xmlReaderSub.Close();
						}
						else if (string.Compare(xmlReader.Name, "mrk", StringComparison.OrdinalIgnoreCase) == 0)
						{
							var xmlReaderSub = xmlReader.ReadSubtree();
							var elements = ReadElements(xmlReaderSub);
							if (elements?.Count > 0)
							{
								source.Elements.AddRange(elements);
							}
						}
						break;
					case XmlNodeType.Whitespace:
						{
							var whiteSpace = new ElementText
							{
								Text = xmlReader.Value
							};

							source.Elements.Add(whiteSpace);
						}
						break;
					case XmlNodeType.Text:
						{
							var text = new ElementText
							{
								Text = xmlReader.Value
							};

							source.Elements.Add(text);
						}
						break;		
				}
			}

			return source;
		}

		private Target ReadTarget(XmlReader xmlReader)
		{
			var target = new Target();

			while (xmlReader.Read())
			{
				switch (xmlReader.NodeType)
				{
					case XmlNodeType.Element:
						if (string.Compare(xmlReader.Name, "bpt", StringComparison.OrdinalIgnoreCase) == 0)
						{
							var xmlReaderSub = xmlReader.ReadSubtree();
							target.Elements.Add(ReadOpeningElementTagPair(xmlReaderSub));
							xmlReaderSub.Close();
						}
						else if (string.Compare(xmlReader.Name, "ept", StringComparison.OrdinalIgnoreCase) == 0)
						{
							var xmlReaderSub = xmlReader.ReadSubtree();
							target.Elements.Add(ReadClosingElementTagPair(xmlReaderSub));
							xmlReaderSub.Close();
						}
						else if (string.Compare(xmlReader.Name, "ph", StringComparison.OrdinalIgnoreCase) == 0)
						{
							var xmlReaderSub = xmlReader.ReadSubtree();
							target.Elements.Add(ReadElementPlaceholder(xmlReaderSub));
							xmlReaderSub.Close();
						}
						else if (string.Compare(xmlReader.Name, "mrk", StringComparison.OrdinalIgnoreCase) == 0)
						{
							var xmlReaderSub = xmlReader.ReadSubtree();
							var elements = ReadElements(xmlReaderSub);
							if (elements?.Count > 0)
							{
								target.Elements.AddRange(elements);
							}

							xmlReaderSub.Close();
						}
						break;
					case XmlNodeType.Whitespace:
						{
							var whiteSpace = new ElementText
							{
								Text = xmlReader.Value
							};

							target.Elements.Add(whiteSpace);
						}
						break;
					case XmlNodeType.Text:
						{
							var text = new ElementText
							{
								Text = xmlReader.Value
							};

							target.Elements.Add(text);
						}
						break;
				}
			}

			return target;
		}

		private ElementTagPair ReadOpeningElementTagPair(XmlReader xmlReader)
		{
			var elementTagPair = new ElementTagPair
			{
				Type = Element.TagType.OpeningTag
			};

			var index = 0;
			while (xmlReader.Read())
			{
				switch (xmlReader.NodeType)
				{
					case XmlNodeType.Element:
						if (index == 0 && string.Compare(xmlReader.Name, "bpt", StringComparison.OrdinalIgnoreCase) == 0)
						{
							index++;
							while (xmlReader.MoveToNextAttribute())
							{
								if (string.Compare(xmlReader.Name, "id", StringComparison.OrdinalIgnoreCase) == 0)
								{
									elementTagPair.TagId = xmlReader.Value;
								}
								if (string.Compare(xmlReader.Name, "equiv-text", StringComparison.OrdinalIgnoreCase) == 0)
								{
									elementTagPair.DisplayText = xmlReader.Value;
								}
							}
						}
						break;
					case XmlNodeType.Whitespace:
						elementTagPair.TagContent += xmlReader.Value;
						break;
					case XmlNodeType.Text:
						elementTagPair.TagContent += xmlReader.Value;
						break;
				}
			}

			return elementTagPair;
		}

		private ElementTagPair ReadClosingElementTagPair(XmlReader xmlReader)
		{
			var elementTagPair = new ElementTagPair
			{
				Type = Element.TagType.ClosingTag
			};

			var index = 0;
			while (xmlReader.Read())
			{
				switch (xmlReader.NodeType)
				{
					case XmlNodeType.Element:
						if (index == 0 && string.Compare(xmlReader.Name, "ept", StringComparison.OrdinalIgnoreCase) == 0)
						{
							index++;
							while (xmlReader.MoveToNextAttribute())
							{
								if (string.Compare(xmlReader.Name, "id", StringComparison.OrdinalIgnoreCase) == 0)
								{
									elementTagPair.TagId = xmlReader.Value;
								}
								if (string.Compare(xmlReader.Name, "equiv-text", StringComparison.OrdinalIgnoreCase) == 0)
								{
									elementTagPair.DisplayText = xmlReader.Value;
								}
							}
						}
						break;
					case XmlNodeType.Whitespace:
						elementTagPair.TagContent += xmlReader.Value;
						break;
					case XmlNodeType.Text:
						elementTagPair.TagContent += xmlReader.Value;
						break;
				}
			}

			return elementTagPair;
		}

		private ElementPlaceholder ReadElementPlaceholder(XmlReader xmlReader)
		{
			var placeholder = new ElementPlaceholder();

			var index = 0;
			while (xmlReader.Read())
			{
				switch (xmlReader.NodeType)
				{
					case XmlNodeType.Element:
						if (index == 0 && string.Compare(xmlReader.Name, "ph", StringComparison.OrdinalIgnoreCase) == 0)
						{
							index++;
							while (xmlReader.MoveToNextAttribute())
							{
								if (string.Compare(xmlReader.Name, "id", StringComparison.OrdinalIgnoreCase) == 0)
								{
									// for polyglot support, the segment id is represented by the trans-unit id
									placeholder.TagId = xmlReader.Value;
								}
								if (string.Compare(xmlReader.Name, "equiv-text", StringComparison.OrdinalIgnoreCase) == 0)
								{
									placeholder.DisplayText = xmlReader.Value;
								}
							}
						}
						break;
					case XmlNodeType.Whitespace:
						placeholder.TagContent += xmlReader.Value;
						break;
					case XmlNodeType.Text:
						placeholder.TagContent += xmlReader.Value;
						break;
				}
			}

			return placeholder;
		}

		private List<Element> ReadElements(XmlReader xmlReader)
		{
			var mtype = string.Empty;
			var mid = string.Empty;
			var cid = string.Empty;

			var elements = new List<Element>();

			var index = 0;
			while (xmlReader.Read())
			{
				switch (xmlReader.NodeType)
				{
					case XmlNodeType.Element:
						if (index == 0 && string.Compare(xmlReader.Name, "mrk", StringComparison.OrdinalIgnoreCase) == 0)
						{
							index++;
							while (xmlReader.MoveToNextAttribute())
							{
								if (string.Compare(xmlReader.Name, "mid", StringComparison.OrdinalIgnoreCase) == 0)
								{
									mid = xmlReader.Value;
								}
								if (string.Compare(xmlReader.Name, "cid", StringComparison.OrdinalIgnoreCase) == 0)
								{
									cid = xmlReader.Value;
								}
								if (string.Compare(xmlReader.Name, "mtype", StringComparison.OrdinalIgnoreCase) == 0)
								{
									mtype = xmlReader.Value;
								}
							}
						}
						if (string.Compare(xmlReader.Name, "bpt", StringComparison.OrdinalIgnoreCase) == 0)
						{
							var xmlReaderSub = xmlReader.ReadSubtree();
							elements.Add(ReadOpeningElementTagPair(xmlReaderSub));
							xmlReaderSub.Close();
						}
						else if (string.Compare(xmlReader.Name, "ept", StringComparison.OrdinalIgnoreCase) == 0)
						{
							var xmlReaderSub = xmlReader.ReadSubtree();
							elements.Add(ReadClosingElementTagPair(xmlReaderSub));
							xmlReaderSub.Close();
						}
						else if (string.Compare(xmlReader.Name, "ph", StringComparison.OrdinalIgnoreCase) == 0)
						{
							var xmlReaderSub = xmlReader.ReadSubtree();
							elements.Add(ReadElementPlaceholder(xmlReaderSub));
							xmlReaderSub.Close();
						}
						else if (string.Compare(xmlReader.Name, "mrk", StringComparison.OrdinalIgnoreCase) == 0)
						{
							var xmlReaderSub = xmlReader.ReadSubtree();
							var elements1 = ReadElements(xmlReaderSub);
							if (elements1?.Count > 0)
							{
								elements1.AddRange(elements);
							}

							xmlReaderSub.Close();
						}
						break;
					case XmlNodeType.Whitespace:
						{
							var whiteSpace = new ElementText
							{
								Text = xmlReader.Value
							};

							elements.Add(whiteSpace);
						}
						break;
					case XmlNodeType.Text:
						{
							var text = new ElementText
							{
								Text = xmlReader.Value
							};

							elements.Add(text);
						}
						break;
				}
			}


			if (string.Compare(mtype, "protected", StringComparison.InvariantCultureIgnoreCase) == 0)
			{
				elements.Insert(0, new ElementLocked
				{					
					Type = Element.TagType.OpeningTag
				});
				elements.Add(new ElementLocked
				{					
					Type = Element.TagType.ClosingTag
				});
			}
			if (string.Compare(mtype, "seg", StringComparison.InvariantCultureIgnoreCase) == 0)
			{
				elements.Insert(0, new ElementSegment
				{
					Id = mid,
					Type = Element.TagType.OpeningTag
				});
				elements.Add(new ElementSegment
				{
					Id = cid,
					Type = Element.TagType.ClosingTag
				});
			}
			if (string.Compare(mtype, "x-sdl-comment", StringComparison.InvariantCultureIgnoreCase) == 0)
			{
				elements.Insert(0, new ElementComment
				{
					Id = cid,
					Type = Element.TagType.OpeningTag
				});
				elements.Add(new ElementComment
				{
					Id = cid,
					Type = Element.TagType.ClosingTag
				});
			}

			return elements;
		}

		private static string ReadText(XmlReader xmlReaderInner)
		{
			var value = string.Empty;

			while (xmlReaderInner.Read())
			{
				switch (xmlReaderInner.NodeType)
				{
					case XmlNodeType.Element:
						//not required for this implementation
						break;
					case XmlNodeType.Whitespace:
						value += xmlReaderInner.Value;
						break;
					case XmlNodeType.Text:
						value += xmlReaderInner.Value;
						break;
					case XmlNodeType.CDATA:
						value += xmlReaderInner.Value;
						break;
					case XmlNodeType.EntityReference:
						value += xmlReaderInner.Name;
						break;
					case XmlNodeType.EndElement:
						//not required for this implementation
						break;
				}
			}
			return value;

		}
	}
}
