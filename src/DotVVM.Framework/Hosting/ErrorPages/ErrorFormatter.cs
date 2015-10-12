﻿using DotVVM.Framework.Exceptions;
using Microsoft.Owin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DotVVM.Framework.Hosting.ErrorPages
{
    public class ErrorFormatter
    {
        public ExceptionModel LoadException(Exception exception, StackFrameModel[] existingTrace = null)
        {
            var m = new ExceptionModel();
            m.Message = exception.Message;
            m.OriginalException = exception;
            m.TypeName = exception.GetType().FullName;
            var s = new StackTrace(exception, true);
            m.Stack = new StackFrameModel[s.FrameCount];
            bool skipping = existingTrace != null;
            for (int i = s.FrameCount - 1; i >= 0; i--)
            {
                var f = s.GetFrame(i);
                if (skipping && existingTrace.Length > i && f.GetMethod() == existingTrace[i].Method) continue;
                skipping = false;

                m.Stack[i] = new StackFrameModel
                {
                    Method = f.GetMethod(),
                    At = LoadSourcePiece(f.GetFileName(), f.GetFileLineNumber(),
                        errorColumn: f.GetFileColumnNumber())
                };

                m.AdditionalInfo = InfoLoaders.Select(info => info(exception)).Where(info => info != null).ToArray();
            }
            if (exception.InnerException != null) m.InnerException = LoadException(exception.InnerException, m.Stack);
            return m;
        }

        public List<Func<Exception, ExceptionAdditionalInfo>> InfoLoaders = new List<Func<Exception, ExceptionAdditionalInfo>>();

        public void AddInfoLoader<T>(Func<T, ExceptionAdditionalInfo> func)
            where T : Exception
        {
            InfoLoaders.Add(e =>
            {
                if (e is T) return func((T)e);
                else return null;
            });
        }

        public static SourceModel LoadSourcePiece(string fileName, int lineNumber,
            int additionalLineCount = 7,
            int errorColumn = 0,
            int errorLength = 0)
        {
            var result = new SourceModel();
            result.FileName = fileName;
            result.LineNumber = lineNumber;
            result.ErrorColumn = errorColumn;
            result.ErrorLength = errorLength;
            try
            {
                var lines = File.ReadAllLines(fileName);
                result.CurrentLine = lines[lineNumber - 1];
                result.PreLines = lines.Skip(lineNumber - additionalLineCount).TakeWhile(l => l != result.CurrentLine).ToArray();
                result.PostLines = lines.Skip(lineNumber).Take(additionalLineCount).ToArray();
                return result;
            }
            catch (Exception)
            {
                result.LoadFailed = true;
                return result;
            }
        }

        public List<Func<Exception, IOwinContext, IErrorSectionFormatter>> Formatters = new List<Func<Exception, IOwinContext, IErrorSectionFormatter>>();

        public string ErrorHtml(Exception exception, IOwinContext owin)
        {
            var template = new ErrorPageTemplate();
            template.Formatters = Formatters.Select(f => f(exception, owin)).Where(t => t != null).ToArray();
            template.ErrorCode = owin.Response.StatusCode;
            template.ErrorDescription = "unhandled exception occured";
            template.Summary = exception.GetType().FullName + ": " + LimitLength(exception.Message, 200);

            return template.TransformText();
        }

        public static ErrorFormatter CreateDefault()
        {
            var f = new ErrorFormatter();
            f.Formatters.Add((e, o) => DotvvmMarkupErrorSection.Create(e));
            f.Formatters.Add((e, o) => new ExceptionSectionFormatter { Exception = f.LoadException(e) });
            f.Formatters.Add((e, o) => DictionarySection.Create("Cookies", "cookies", o.Request.Cookies));
            f.Formatters.Add((e, o) => DictionarySection.Create("Request headers", "reqHeaders", o.Request.Headers));
            f.Formatters.Add((e, o) => DictionarySection.Create("Environment", "env", o.Environment));
            f.AddInfoLoader<ReflectionTypeLoadException>(e => new ExceptionAdditionalInfo
            {
                Title = "Loader Exceptions",
                Objects = e.LoaderExceptions.Select(lde => lde.GetType().Name + ": " + lde.Message).ToArray(),
                Display = ExceptionAdditionalInfo.DisplayMode.ToString
            });
            f.AddInfoLoader<DotvvmCompilationException>(e => new ExceptionAdditionalInfo
            {
                Title = "DotVVM compilation",
                Objects = new object[] {
                    $"error in '{ string.Concat(e.Tokens.Select(t => t.Text)) }' at line { e.Tokens.First().LineNumber } in { e.FileName }"
                },
                Display = ExceptionAdditionalInfo.DisplayMode.ToString
            });
            
            return f;
        }

        public string LimitLength(string source, int length, string ending = "...")
        {
            if (length < source.Length)
            {
                return source.Substring(length - ending.Length) + ending;
            }
            else
            {
                return source;
            }
        }
    }
}
