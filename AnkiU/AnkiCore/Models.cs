using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using System.Text.RegularExpressions;
using AnkiU.AnkiCore.Templates;
using System.Diagnostics;

namespace AnkiU.AnkiCore
{
    class Models
    {
        private static readonly Regex fClozePattern1 = new Regex(@"{{[^}]*? cloze:(?:[^}]?:)*(.+?)}}", RegexOptions.Compiled);
        private static readonly Regex fClozePattern2 = new Regex(@"<%cloze:(.+?)%>", RegexOptions.Compiled);
        private static readonly Regex fClozeOrdPattern = new Regex(@"{{c(\d+)::.+?}}", RegexOptions.Compiled);

        public static readonly string defaultModel =
            "{'sortf': 0, "
            + "'did': 1, "
            + "'latexPre': \""
            + "\\\\documentclass[12pt]{article}\\n"
            + "\\\\special{papersize=3in,5in}\\n"
            + "\\\\usepackage[utf8]{inputenc}\\n"
            + "\\\\usepackage{amssymb,amsmath}\\n"
            + "\\\\pagestyle{empty}\\n"
            + "\\\\setlength{\\\\parindent}{0in}\\n"
            + "\\\\begin{document}\\n"
            + "\", "
            + "'latexPost': \"\\\\end{document}\", "
            + "'mod': 0, "
            + "'usn': 0, "
            + "'vers': [], " // FIXME: remove when other clients have caught up
            + "'type': "
            + ModelType.STD
            + ", "
            + "'css': \".card {\\n"
            + " font-family: arial;\\n"
            + " font-size: 20px;\\n"
            + " text-align: center;\\n"
            + " color: black;\\n"
            + " background-color: white;\\n"
            + "}\""
            + "}";

        private const string defaultField = "{'name': \"\", " + "'ord': null, " + "'sticky': False, " +
            // the following alter editing, and are used as defaults for the template wizard
            "'rtl': False, " + "'font': \"Arial\", " + "'size': 20, " +
            // reserved for future use
            "'media': [] }";

        private const string defaultTemplate = "{'name': \"\", " + "'ord': null, " + "'qfmt': \"\", "
            + "'afmt': \"\", " + "'did': null, " + "'bqfmt': \"\"," + "'bafmt': \"\"," + "'bfont': \"Arial\"," +
            "'bsize': 12 }";

        private Collection collection;
        private bool isChanged;
        private JsonObject models;

        private int id;
        private string name = "";
        private long crt = DateTimeOffset.Now.ToUnixTimeSeconds();
        private long modifiedTime = DateTimeOffset.Now.ToUnixTimeSeconds();
        private JsonObject conf;
        private string css = "";
        private JsonArray fields;
        private JsonArray templates;

        private Dictionary<string, Template> compldTemplateMap = new Dictionary<string, Template>();

        public delegate List<string> TransForm(List<string> fields);

        public Models(Collection col)
        {
            this.collection = col;
        }

        public void Load(string json)
        {
            isChanged = false;
            models = JsonObject.Parse(json);
        }

        /// <summary>
        /// Save a model
        /// </summary>
        /// <param name="m">model to save</param>
        /// <param name="template">templates flag which (when true) 
        /// re-generates the cards for each note which uses the model</param>
        public void Save(JsonObject m = null, bool template = false)
        {
            if (m != null && m.ContainsKey("id"))
            {
                m.Add("mod",
                    JsonValue.CreateNumberValue(DateTimeOffset.Now.ToUnixTimeSeconds()));
                m.Add("usn", JsonValue.CreateNumberValue(collection.Usn));
                if (m.GetNamedNumber("id") != 0)
                    UpdateRequired(m);
                else
                    Debug.WriteLine("TODO: fix empty id problem on UpdateRequired(needed for model adding)");

                if (template)
                    SyncTemplates(m); //Continue
            }
            isChanged = true;
        }

        private void UpdateRequired(JsonObject m)
        {
            if (m.GetNamedNumber("type") == (int)ModelType.CLOZE)
                return;

            JsonArray req = new JsonArray();
            List<string> flds = new List<string>();
            JsonArray fields = m.GetNamedArray("flds");
            for (uint i = 0; i < fields.Count; i++)
                flds.Add(fields.GetObjectAt(i).GetNamedString("name"));

            JsonArray templates = m.GetNamedArray("tmpls");
            for (uint i = 0; i < templates.Count; i++)
            {
                JsonObject t = templates.GetObjectAt(i);
                ReqTemplateStruct ret = RequestForTemplate(m, flds, t);
                JsonArray r = new JsonArray();
                r.Add(JsonValue.CreateNumberValue(t.GetNamedNumber("ord")));
                r.Add(JsonValue.CreateStringValue(ret.Type));
                foreach (JsonArray a in ret.Request)
                    r.Add(a);
                req.Add(r);
            }
            m.Add("req", req);
        }

        /// <summary>
        /// A struct used to wrap the return results of RequestForTemplate
        /// instead of using object[] like the java source code.
        /// This is done to avoid boxing/unboxing and promote type-safe
        /// when results are used in UpdateRequired().
        /// </summary>
        private struct ReqTemplateStruct
        {
            public string Type { get; set; }
            public JsonArray[] Request { get; set; }
        }

        private ReqTemplateStruct RequestForTemplate(JsonObject m, List<string> flds, JsonObject t)
        {
            List<string> a = new List<string>();
            List<string> b = new List<string>();
            ReqTemplateStruct returnResult = new ReqTemplateStruct();
            foreach (string f in flds)
            {
                a.Add("ankiflag");
                b.Add("");
            }
            object[] data = new object[] {1L, 1L, (long)m.GetNamedNumber("id"), 1L, (int)t.GetNamedNumber("ord"), "",
                                Utils.JoinFields(a.ToArray()) };
            string full = collection.RenderQA(data)["q"];
            data = new object[] {1L, 1L, (long)m.GetNamedNumber("id"), 1L, (int)t.GetNamedNumber("ord"), "",
                                Utils.JoinFields(b.ToArray()) };
            string empty = collection.RenderQA(data)["q"];
            // if full and empty are the same, the template is invalid and there is no way to satisfy it
            if (full.Equals(empty))
            {
                returnResult.Type = "none";
                returnResult.Request = new JsonArray[2] { new JsonArray(), new JsonArray() };
                return returnResult;
            }
            returnResult.Type = "all";
            returnResult.Request = new JsonArray[1];
            List<string> tmp = new List<string>();
            for (int i = 0; i < flds.Count; i++)
            {
                tmp.Clear();
                tmp.AddRange(a);
                tmp[i] = "";
                data[6] = Utils.JoinFields(tmp.ToArray());
                // if no field content appeared, field is required
                if (!collection.RenderQA(data)["q"].Contains("ankiflag"))
                    returnResult.Request[0].Add(JsonValue.CreateNumberValue(i));
            }
            if (returnResult.Request[0].Count > 0)
                return returnResult;

            // if there are no required fields, switch to any mode
            returnResult.Type = "any";
            returnResult.Request = new JsonArray[1];
            for (int i = 0; i < flds.Count; i++)
            {
                tmp.Clear();
                tmp.AddRange(b);
                tmp[i] = "1";
                data[6] = Utils.JoinFields(tmp.ToArray());
                // if not the same as empty, this field can make the card non-blank
                if (!collection.RenderQA(data)["q"].Equals(empty))
                    returnResult.Request[0].Add(JsonValue.CreateNumberValue(i));
            }
            return returnResult;
        }

        public JsonObject get(long mid)
        {
            throw new NotImplementedException();
        }

        private void SyncTemplates(JsonObject m)
        {
            List<long> rem = collection.GenCards(GetNoteIds(m).ToArray());
        }

        public List<long> GetNoteIds(JsonObject m)
        {
            var notes = collection.Database.QueryColumn<notes>(
                            "SELECT id FROM notes WHERE mid = " + (long)m.GetNamedNumber("id"));
            return (from s in notes select s.Id).ToList();
        }

        /// <summary>
        /// Flush the registry if any models were changed.
        /// </summary>
        public void flush()
        {
            if (isChanged)
            {
                collection.Database.Execute("update col set models = ?", Utils.JsonToString(models));
                isChanged = false;
            }
        }

        /// <summary>
        /// Get current model.
        /// </summary>
        /// <param name="forDeck">If true, it tries to get the deck specified in deck by mid, 
        /// otherwise or if the former is not found, it uses the configuration`s field curModel.</param>
        /// <returns>The JSONObject of the model, or null if not found in the deck and in the configuration.</returns>
        public JsonObject Current(bool forDeck = true)
        {
            JsonObject m = null;
            if (forDeck)
            {
                m = GetJsonModelByLong(collection.Decks.Current(), "mid");
            }
            if (m == null)
            {
                m = GetJsonModelByLong(collection.GetConf(), "curModel");
            }
            if (m == null)
            {
                if (models.Count != 0)
                {
                    m = models.Values.ToArray()[0] as JsonObject;
                }
            }
            return m;
        }

        /// <summary>
        /// Get JsonObject by first get a long number in the input JsonObject
        /// by name. Then this number is used to get the model object
        /// </summary>
        /// <param name="jObj">A JsonObject</param>
        /// <param name="name">The name used to search for the number</param>
        /// <returns>A JsonObject or null if not found</returns>
        private JsonObject GetJsonModelByLong(JsonObject jObj, string name)
        {
            //Since value is type long, -1 is used to mark as not found
            //We can not use null due to API restriction 
            double mid = jObj.GetNamedNumber(name, -1);
            if (mid != -1)
                return Get((long)mid);

            return null;
        }

        /// <summary>
        /// Get model with ID, or none.
        /// </summary>
        /// <param name="id"></param>
        /// <returns>A JsonObject or null if not found</returns>
        public JsonObject Get(long id)
        {
            string idStr = id.ToString();
            if (models.ContainsKey(idStr))
            {
                return models[idStr] as JsonObject;
            }
            else
            {
                return null;
            }
        }

        public void SetCurrent(JsonObject m)
        {
            collection.GetConf().Add("curModel", m.GetNamedValue("id"));
            collection.SetMod();
        }

        public List<JsonObject> All()
        {
            return models.Values as List<JsonObject>;
        }

        public JsonObject ByName(string name)
        {
            foreach (JsonObject m in All())
            {
                if (m.GetNamedString("name").Equals(name))
                    return m;
            }
            return null;
        }

        /// <summary>
        /// Create a new model, save it in the registry, and return it.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public JsonObject newModel(string name)
        {
            JsonObject m = JsonObject.Parse(defaultModel);
            m.Add("name", JsonValue.CreateStringValue(name));
            m.Add("mod", JsonValue.CreateNumberValue(
                 DateTimeOffset.Now.ToUnixTimeSeconds()));
            m.Add("flds", new JsonArray());
            m.Add("tmpls", new JsonArray());
            m.Add("tags", new JsonArray());
            m.Add("id", JsonValue.CreateNumberValue(0));
            return m;
        }

        /// <summary>
        /// Delete model, and all its cards/notes.
        /// </summary>
        /// <param name="m"></param>
        /// <returns>Throw ConfirmModSchemaException.</returns>
        public void Remove(JsonObject m)
        {
            collection.ModSchema(true);
            double id = m.GetNamedNumber("id");
            bool current = Current().GetNamedNumber("id") == id;
            string idStr = id.ToString();
            var list = collection.Database.QueryColumn<Card>(
                "SELECT id FROM cards WHERE nid IN(SELECT id FROM notes WHERE mid = " + idStr + ")");
            var idArray = (from s in list select s.Id).ToArray();
            collection.RemoveCards(idArray);
            models.Remove(idStr);
            Save();
            if (current)
                SetCurrent(models.Values.ToArray()[0] as JsonObject);
        }

        public void Add(JsonObject m)
        {
            SetId(m);
            Update(m);
            SetCurrent(m);
            Save(m);
        }

        private void SetId(JsonObject m)
        {
            long id = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            while (models.ContainsKey(id.ToString()))
            {
                id = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            }
            m.Add("id", JsonValue.CreateNumberValue(id));
        }

        public void Update(JsonObject m)
        {
            models.Add(m.GetNamedNumber("id").ToString(), m);
            Save();
        }

        public bool Have(long id)
        {
            return models.ContainsKey(id.ToString());
        }

        //TODO: Check whether we need to return long[]
        //or string[]
        public long[] ids()
        {
            long[] ids = new long[models.Count];
            int i = 0;
            foreach (string idStr in models.Keys)
            {
                ids[i] = Convert.ToInt64(idStr);
                i++;
            }
            return ids;
        }

        public List<long> NoteIds(JsonObject model)
        {
            string sql = "SELECT id FROM notes WHERE mid = " + model.GetNamedValue("id");
            var list = collection.Database.QueryColumn<notes>(sql);
            return (from s in list select s.Id).ToList();
        }

        /// <summary>
        /// Number of notes using m
        /// </summary>
        /// <param name="model">The model to the count the notes of</param>
        /// <returns>The number of notes with that model</returns>
        public int UseCount(JsonObject model)
        {
            return collection.Database.QueryScalar<int>("select count() from notes where mid = " + model.GetNamedNumber("id"));
        }

        /// <summary>
        /// Number of notes using m
        /// </summary>
        /// <param name="model">The model to the count the notes of</param>
        /// <param name="ord">The index of the card template</param>
        /// <returns>The number of notes with that model</returns>
        public int TmplUseCount(JsonObject model, int ord)
        {
            return collection.Database.QueryScalar<int>(
                "select count() from cards, notes where cards.nid = notes.id and notes.mid = "
                + model.GetNamedNumber("id") + " and cards.ord = " + ord);
        }

        /// <summary>
        /// Copy, save and return
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public JsonObject Copy(JsonObject model)
        {
            JsonObject m = null;
            m = JsonObject.Parse(Utils.JsonToString(model));
            m.Add("name", JsonValue.CreateStringValue(
                           (m.GetNamedString("name") + " copy")));
            Add(m);
            return m;
        }

        public JsonObject newField(string name)
        {
            JsonObject f;
            f = JsonObject.Parse(defaultField);
            f.Add("name", JsonValue.CreateStringValue(name));
            return f;
        }

        /// <summary>
        /// Mapping of field name -> (ord, field).
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public Dictionary<string, KeyValuePair<int, JsonObject>> FieldMap(JsonObject model)
        {
            JsonArray ja;
            ja = model.GetNamedArray("flds");
            var result = new Dictionary<string, KeyValuePair<int, JsonObject>>();
            for (uint i = 0; i < ja.Count; i++)
            {
                JsonObject f = ja.GetObjectAt(i);
                result.Add(f.GetNamedString("name"),
                    new KeyValuePair<int, JsonObject>((int)f.GetNamedNumber("ord"), f));
            }
            return result;
        }

        public List<string> FieldNames(JsonObject model)
        {
            JsonArray ja;
            ja = model.GetNamedArray("flds");
            List<string> names = new List<string>();
            for (uint i = 0; i < ja.Count; i++)
            {
                names.Add(ja.GetObjectAt(i).GetNamedString("name"));
            }
            return names;
        }

        public int SortIdx(JsonObject model)
        {
            return (int)model.GetNamedNumber("sortf");
        }

        public void SetSortIdx(JsonObject model, int idx)
        {
            collection.ModSchema(true);
            model.Add("sortf", JsonValue.CreateNumberValue(idx));
            collection.UpdateFieldCache((NoteIds(model).ToArray()));
            Save(model);
        }

        public void AddField(JsonObject model, JsonObject field)
        {
            // only mod schema if model isn't new
            if (model.GetNamedNumber("id") != 0)
                collection.ModSchema(true);

            JsonArray ja = model.GetNamedArray("flds");
            ja.Add(field);
            model.Add("flds", ja);
            UpdateFieldOrds(model);
            Save(model);
            TransformFields(model, (List<string> f) =>
            {
                List<string> l = new List<string>(f);
                l.Add("");
                return l;
            });
        }

        public void UpdateFieldOrds(JsonObject m)
        {
            JsonArray ja;
            ja = m.GetNamedArray("flds");
            for (uint i = 0; i < ja.Count; i++)
            {
                JsonObject f = ja.GetObjectAt(i);
                f.Add("ord", JsonValue.CreateNumberValue(i));
            }
        }

        public void TransformFields(JsonObject model, TransForm fn)
        {
            if (model.GetNamedNumber("id") == 0)
                return;

            List<object[]> r = new List<object[]>();
            var list = collection.Database.QueryColumn<notes>("select id, flds from notes where mid = ?", model.GetNamedNumber("id"));

            foreach (notes note in list)
            {
                r.Add(new object[] {
                    Utils.JoinFields(fn(Utils.SplitFields(note.Fields).ToList())),
                    DateTimeOffset.Now.ToUnixTimeSeconds(), collection.Usn, note.Id});
            }

            collection.Database.ExecuteMany("update notes set flds=?,mod=?,usn=? where id = ?", r);
        }

        public void RemoveField(JsonObject model, JsonObject field)
        {
            collection.ModSchema(true);
            JsonArray ja = model.GetNamedArray("flds");
            JsonArray ja2 = new JsonArray();
            int idx = -1;
            for (int i = 0; i < ja.Count; ++i)
            {
                if (field.Equals(ja.GetObjectAt((uint)i)))
                {
                    idx = i;
                    continue;
                }
                ja2.Add(ja[i]);
            }

            if (idx < 0)
                throw new Exception("Mode.RemoveFiled: Can't find field to remove");

            model.Add("flds", ja2);
            int sortf = (int)model.GetNamedNumber("sortf");
            if (sortf >= model.GetNamedArray("flds").Count)
            {
                model.Add("sortf", JsonValue.CreateNumberValue(sortf - 1));
            }
            UpdateFieldOrds(model);
            TransformFields(model, (List<string> f) =>
            {
                List<string> l = new List<string>(f);
                l.RemoveAt(idx);
                return l;
            });

            if (idx == SortIdx(model))
            {
                // need to rebuild
                collection.UpdateFieldCache(NoteIds(model).ToArray());
            }
            RenameField(model, field, null);
        }

        public void RenameField(JsonObject model, JsonObject field, string newName)
        {
            collection.ModSchema(true);
            string pattern = string.Format("\\{\\{([^{}]*)([:#^/]|[^:#/^}][^:}]*?:|){0}\\}\\}",
                                                field.GetNamedString(name));

            if (newName == null)
                newName = "";

            string repl = "{{$1$2" + newName + "}}";

            JsonArray tmpls = model.GetNamedArray("tmpls");
            for (uint i = 0; i < tmpls.Count; ++i)
            {
                JsonObject t = tmpls.GetObjectAt(i);
                foreach (string fmt in new string[] { "qfmt", "afmt" })
                {
                    string str = t.GetNamedString(fmt);
                    if (!newName.Equals(""))
                        t.Add(fmt, JsonValue.CreateStringValue(str.Replace(pattern, repl)));
                    else
                        t.Add(fmt, JsonValue.CreateStringValue(str.Replace(pattern, "")));
                }
            }
            field.Add("name", JsonValue.CreateStringValue(newName));
            Save(model);
        }

        public void MoveField(JsonObject m, JsonObject field, int idx)
        {
            collection.ModSchema(true);
            JsonArray ja = m.GetNamedArray("flds");
            List<JsonObject> l = new List<JsonObject>();
            int oldidx = -1;
            for (uint i = 0; i < ja.Count; ++i)
            {
                l.Add(ja.GetObjectAt(i));
                if (field.Equals(ja.GetObjectAt(i)))
                {
                    oldidx = (int)i;
                    if (idx == oldidx)
                        return;
                }
            }
            if (oldidx == -1)
                throw new Exception("Models.MoveField: Can't find the specified field!");

            // remember old sort field
            string sortf = Utils.JsonToString(m.GetNamedArray("flds").GetObjectAt((uint)m.GetNamedNumber("sortf")));

            // move
            l.RemoveAt(oldidx);
            l.Insert(idx, field);
            m.Add("flds", l.ToJsonArray());

            // restore sort field
            ja = m.GetNamedArray("flds");
            for (uint i = 0; i < ja.Count; ++i)
            {
                if (Utils.JsonToString(ja.GetObjectAt(i)).Equals(sortf))
                {
                    m.Add("sortf", JsonValue.CreateNumberValue(i));
                    break;
                }
            }
            UpdateFieldOrds(m);
            Save(m);
            TransformFields(m, (List<string> fields) => 
            {
                string val = fields[oldidx];
                List<string> fl = new List<string>(fields);
                fl.RemoveAt(oldidx);
                fl.Insert(idx, val);
                return fl;
            });

            //TODO: Continue newTemplate()
        }
    }
}
            
        
            
    

  
