/*
Copyright (C) 2016 Anki Universal Team <ankiuniversal@outlook.com>

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as
published by the Free Software Foundation, either version 3 of the
License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

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
    public class Models
    {
        private static readonly Regex fClozePattern1 = new Regex(@"{{[^}]*?cloze:(?:[^}]?:)*(.+?)}}", RegexOptions.Compiled);
        private static readonly Regex fClozePattern2 = new Regex(@"<%cloze:(.+?)%>", RegexOptions.Compiled);
        private static readonly Regex fClozeOrdPattern = new Regex(@"{{c(\d+)::.+?}}", RegexOptions.Compiled);

        public static readonly string defaultModel =
            "{\"sortf\": 0, "
            + "\"did\": 1, "
            + "\"latexPre\": \""
            + "\\\\documentclass[12pt]{article}\\n"
            + "\\\\special{papersize=3in,5in}\\n"
            + "\\\\usepackage[utf8]{inputenc}\\n"
            + "\\\\usepackage{amssymb,amsmath}\\n"
            + "\\\\pagestyle{empty}\\n"
            + "\\\\setlength{\\\\parindent}{0in}\\n"
            + "\\\\begin{document}\\n"
            + "\", "
            + "\"latexPost\": \"\\\\end{document}\", "
            + "\"mod\": 0, "
            + "\"usn\": 0, "
            + "\"vers\": [], " // FIXME: remove when other clients have caught up
            + "\"type\": "
            + (int)ModelType.STD
            + ", "
            + "\"css\": \".card {\\n"
            + " font-family: sans-serif;\\n"
            + " font-size: 20px;\\n"
            + " text-align: center;\\n"
            + " color: black;\\n"
            + " background-color: white;\\n"
            + "}\""
            + "}";

        private const string defaultField = "{\"name\": \"\", " + "\"ord\": null, " + "\"sticky\": false, " +
            // the following alter editing, and are used as defaults for the template wizard
            "\"rtl\": false, " + "\"font\": \"Fira Sans\", " + "\"size\": 20, " +
            // reserved for future use
            "\"media\": [] }";

        private const string defaultTemplate = "{\"name\": \"\", " + "\"ord\": null, " + "\"qfmt\": \"\", "
            + "\"afmt\": \"\", " + "\"did\": null, " + "\"bqfmt\": \"\"," + "\"bafmt\": \"\"," + "\"bfont\": \"Fira Sans\"," +
            "\"bsize\": 12 }";

        private Collection collection;
        private bool isChanged;
        private JsonObject models;
        public JsonObject ThisModels { get { return models; } }

        private string name = "";
        public string Name { get { return name; } }

        private long crt = DateTimeOffset.Now.ToUnixTimeSeconds();
        private long modifiedTime = DateTimeOffset.Now.ToUnixTimeSeconds();

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
                m["mod"] =
                    JsonValue.CreateNumberValue(DateTimeOffset.Now.ToUnixTimeSeconds());
                m["usn"] = JsonValue.CreateNumberValue(collection.Usn);
                if (m.GetNamedNumber("id") != 0)
                    UpdateRequired(m);
                else
                    Debug.WriteLine("TODO: fix empty id problem on UpdateRequired(needed for model adding)");

                if (template)
                    SyncTemplates(m); 
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
                r.Add(ret.Request[0]);
                req.Add(r);
            }
            m["req"] = req;
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
                //java and python source return 2 empty jsonarray here. Why?
                returnResult.Request = new JsonArray[] { new JsonArray(), new JsonArray() };
                return returnResult;
            }
            returnResult.Type = "all";
            returnResult.Request = new JsonArray[] { new JsonArray() };
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
            returnResult.Request = new JsonArray[] { new JsonArray() };
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

        private void SyncTemplates(JsonObject m)
        {
            List<long> rem = collection.GenCards(GetNoteIds(m).ToArray());
        }

        public List<long> GetNoteIds(JsonObject m)
        {
            var notes = collection.Database.QueryColumn<NoteTable>(
                            "SELECT id FROM notes WHERE mid = " + (long)m.GetNamedNumber("id"));
            return (from s in notes select s.Id).ToList();
        }

        /// <summary>
        /// Flush the registry if any models were changed.
        /// </summary>
        public void SaveChangesToDatabse()
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
        public JsonObject GetCurrent(bool forDeck = true)
        {
            JsonObject m = null;
            if (forDeck)
            {
                m = GetJsonModelByLong(collection.Deck.Current(), "mid");
            }
            if (m == null)
            {
                if (forDeck)
                {
                    //WARNING: Not in java and python ver
                    //Guess mid by getting a card and used its note to find modelID
                    var card = collection.Database.QueryFirstRow<CardTable>("Select nid from cards where did = ?", collection.Deck.Selected());
                    if (card != null && card.Count != 0)
                    {
                        var note = collection.Database.QueryFirstRow<NoteTable>("Select mid from notes where id = ?", card[0].Nid);
                        m = collection.Models.Get(note[0].Mid);
                    }
                    else
                        m = GetJsonModelByLong(collection.Conf, "curModel");
                }
                else
                    m = GetJsonModelByLong(collection.Conf, "curModel");
            }
            if (m == null)
            {
                if (models.Count != 0)
                {
                    m = models.Values.ToArray()[0].GetObject();
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
                return models.GetNamedObject(idStr);
            }
            else
            {
                return null;
            }
        }

        public void SetCurrent(JsonObject m)
        {
            collection.Conf["curModel"] = m.GetNamedValue("id");
            collection.SetIsModified();
        }

        public void SetCurrent(long id)
        {
            collection.Conf["curModel"] = JsonValue.CreateNumberValue(id);
            collection.SetIsModified();
        }

        public List<JsonObject> All()
        {
            var values = models.Values;
            List<JsonObject> list = new List<JsonObject>();
            foreach (var v in values)
                list.Add(v.GetObject());
            return list;
        }

        public List<string> AllNames()
        {
            List<string> list = new List<string>();
            foreach(JsonObject m in All())
                list.Add(m.GetNamedString("name"));
            return list;
        }

        /// <summary>
        /// Get model with name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public JsonObject GetModelByName(string name)
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
        public JsonObject NewModel(string name)
        {
            JsonObject m = JsonObject.Parse(defaultModel);
            m["name"] = JsonValue.CreateStringValue(name);
            m["mod"] = JsonValue.CreateNumberValue(
                 DateTimeOffset.Now.ToUnixTimeSeconds());
            m["flds"] = new JsonArray();
            m["tmpls"] = new JsonArray();
            m["tags"] = new JsonArray();
            m["id"] = JsonValue.CreateNumberValue(0);
            return m;
        }

        /// <summary>
        /// Delete model, and all its cards/notes.
        /// </summary>
        /// <param name="m"></param>
        /// <returns>Throw ConfirmModSchemaException.</returns>
        public void Remove(JsonObject m, bool forDeck = true)
        {
            collection.ModSchema(true);
            double id = m.GetNamedNumber("id");
            bool current = GetCurrent(forDeck).GetNamedNumber("id") == id;
            string idStr = id.ToString();
            var list = collection.Database.QueryColumn<CardIdOnlyTable>(
                "SELECT id FROM cards WHERE nid IN(SELECT id FROM notes WHERE mid = " + idStr + ")");
            var idArray = (from s in list select s.Id).ToArray();
            collection.RemoveCardsAndNoteIfNoCardsLeft(idArray);
            models.Remove(idStr);
            Save();            
            if (current)
                SetCurrent(models.Values.ToArray()[0].GetObject());
        }

        public void Add(JsonObject m)
        {
            SetId(m);
            Update(m);
            SetCurrent(m);
            Save(m);
        }

        public void EnsureNameUnique(JsonObject model)
        {
            string name;
            var allModels = All();
            if (allModels == null)
                return;
            foreach (JsonObject m in All())
            {
                name = m.GetNamedString("name");
                if ((name == model.GetNamedString("name")) &&
                        m.GetNamedNumber("id") != model.GetNamedNumber("id"))
                {
                    StringBuilder temp = new StringBuilder();
                    temp.Append(name + "-");
                    string checksum = Utils.Checksum(DateTimeOffset.Now.ToUnixTimeSeconds().ToString());
                    temp.Append(checksum, 0, 5);
                    m[name] = JsonValue.CreateStringValue(temp.ToString());
                    break;
                }
            }
        }

        private void SetId(JsonObject m)
        {
            long id = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            while (models.ContainsKey(id.ToString()))
            {
                id = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            }
            m["id"] = JsonValue.CreateNumberValue(id);
        }

        /// <summary>
        /// Add or update an existing model. Used for syncing and merging
        /// </summary>
        /// <param name="m"></param>
        public void Update(JsonObject m)
        {
            //TODO: find out why
            //python ver has this line
            //but java ver does not
            //EnsureNameUnique(m);
            var key = m.GetNamedNumber("id");
            models[key.ToString()] = m;
            Save();
        }

        public bool Have(long id)
        {
            return models.ContainsKey(id.ToString());
        }

        public long[] Ids()
        {
            long[] ids = new long[models.Count];
            int i = 0;
            foreach (string idStr in models.Keys)
            {
                ids[i] = long.Parse(idStr);
                i++;
            }
            return ids;
        }

        public List<long> NoteIds(JsonObject model)
        {
            string sql = "SELECT id FROM notes WHERE mid = " + model.GetNamedValue("id");
            var list = collection.Database.QueryColumn<NoteTable>(sql);
            return (from s in list select s.Id).ToList();
        }

        /// <summary>
        /// Number of notes using m
        /// </summary>
        /// <param name="model">The model to the count the notes of</param>
        /// <returns>The number of notes with that model</returns>
        public int NoteUseCount(JsonObject model)
        {
            return collection.Database.QueryScalar<int>("select count() from notes where mid = " + model.GetNamedNumber("id"));
        }

        /// <summary>
        /// Number of cards using a template of a model
        /// </summary>
        /// <param name="model">The model to count</param>
        /// <param name="ord">The index of the template</param>
        /// <returns>The number of cards generated from the template</returns>
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
            m["name"] = JsonValue.CreateStringValue(
                           (m.GetNamedString("name") + " copy"));
            Add(m);
            return m;
        }

        public JsonObject NewField(string name)
        {
            JsonObject f = JsonObject.Parse(defaultField);
            f["name"] = JsonValue.CreateStringValue(name);
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
            model["sortf"] = JsonValue.CreateNumberValue(idx);
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
            model["flds"] = ja;
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
                f["ord"] = JsonValue.CreateNumberValue(i);
            }
        }

        public void TransformFields(JsonObject model, TransForm fn)
        {
            if (model.GetNamedNumber("id") == 0)
                return;

            List<object[]> r = new List<object[]>();
            var list = collection.Database.QueryColumn<NoteTable>("select id, flds from notes where mid = ?", model.GetNamedNumber("id"));

            foreach (NoteTable note in list)
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
                throw new ModelException("RemoveFiled: Can't find field to remove");

            model["flds"] = ja2;
            int sortf = (int)model.GetNamedNumber("sortf");
            if (sortf >= model.GetNamedArray("flds").Count)
            {
                model["sortf"] = JsonValue.CreateNumberValue(sortf - 1);
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
            string pattern = String.Format("\\{{\\{{([^{{}}]*)([:#^/]|[^:#/^}}][^:}}]*?:|){0}\\}}\\}}",
                                                Regex.Escape(field.GetNamedString("name")));

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
                        t[fmt] = JsonValue.CreateStringValue(Regex.Replace(str, pattern, repl));
                    else
                        t[fmt] = JsonValue.CreateStringValue(Regex.Replace(str, pattern, ""));
                }
            }
            field["name"] = JsonValue.CreateStringValue(newName);
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
                throw new ModelException("MoveField: Can't find the specified field!");

            // remember old sort field
            string sortf = Utils.JsonToString(m.GetNamedArray("flds").GetObjectAt((uint)m.GetNamedNumber("sortf")));

            // move
            l.RemoveAt(oldidx);
            l.Insert(idx, field);
            m["flds"] = l.ToJsonArray();

            // restore sort field
            ja = m.GetNamedArray("flds");
            for (uint i = 0; i < ja.Count; ++i)
            {
                if (Utils.JsonToString(ja.GetObjectAt(i)).Equals(sortf))
                {
                    m["sortf"] = JsonValue.CreateNumberValue(i);
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
        }

        public JsonObject NewTemplate(string name)
        {
            JsonObject t;
            t = JsonObject.Parse(defaultTemplate);
            t["name"] = JsonValue.CreateStringValue(name);
            return t;
        }

        /// <summary>
        /// Note: should col.genCards() afterwards.
        /// </summary>
        /// <param name="m"></param>
        /// <param name="template"></param>
        public void AddTemplate(JsonObject m, JsonObject template) 
        {
            if (m.GetNamedNumber("id") != 0)
                collection.ModSchema(true);
            
            JsonArray ja = m.GetNamedArray("tmpls");
            ja.Add(template);
            m["tmpls"] = ja;
            UpdateTemplOrds(m);
            Save(m);
        }

        public void UpdateTemplOrds(JsonObject m)
        {
            JsonArray ja;
            ja = m.GetNamedArray("tmpls");
            for (uint i = 0; i < ja.Count; i++)
            {
                JsonObject f = ja.GetObjectAt(i);
                f["ord"] = JsonValue.CreateNumberValue(i);
            }
        }

        /// <summary>
        /// Remove a template
        /// </summary>
        /// <param name="model"></param>
        /// <param name="template"></param>
        /// <returns>False if removing template would leave orphan notes</returns>
        public bool RemoveTemplate(JsonObject model, JsonObject template)
        {
            Debug.Assert(model.GetNamedArray("tmpls").Count > 1);

            JsonArray ja = model.GetNamedArray("tmpls"); 
            int ord = -1;
            for(uint i = 0; i < ja.Count; i++)
            {
                if(ja.GetObjectAt(i).Equals(template))
                {
                    ord = (int)i;
                    break;
                }
            }
            if (ord == -1)
                throw new ModelException("RemTemplate: Not found ord!");

            string sql = "select c.id from cards c, notes f where c.nid=f.id and mid = " +
                    model.GetNamedNumber("id") + " and ord = " + ord;
            var cids = (from s in collection.Database.QueryColumn<CardIdOnlyTable>(sql) select s.Id).ToArray();
            // all notes with this template must have at least two cards, 
            // or we could end up creating orphaned notes
            sql = "select nid, count() from cards where nid in (select nid from cards where id in " +
                    Utils.Ids2str(cids) + ") group by nid having count() < 2 limit 1";
            if (collection.Database.QueryScalar<long>(sql) != 0)
                return false;

            // Ok to proceed; remove cards
            collection.ModSchema(true);
            collection.RemoveCardsAndNoteIfNoCardsLeft(cids);
            sql = "update cards set ord = ord - 1, usn = ?, mod = ? where nid in (select id from notes where mid = ?) and ord > ?";
            object[] arrayObject = new object[] { collection.Usn, DateTimeOffset.Now.ToUnixTimeSeconds(), (long)model.GetNamedNumber("id"), ord };
            //Shift ordinals
            collection.Database.Execute(sql, arrayObject);
            JsonArray tmpls = model.GetNamedArray("tmpls");
            JsonArray ja2 = new JsonArray();
            for(uint i = 0; i < tmpls.Count; ++i)
            {
                if (template.Equals(tmpls.GetObjectAt(i)))
                    continue;

                ja2.Add(tmpls[(int)i]);
            }
            model["tmpls"] = ja2;

            UpdateTemplOrds(model);
            Save(model);
            return true;
        }

        public void MoveTemplate(JsonObject m, JsonObject template, int idx)
        {
            JsonArray ja = m.GetNamedArray("tmpls");
            int oldidx = -1;
            List<JsonObject> l = new List<JsonObject>();
            Dictionary<int, int> oldidxs = new Dictionary<int, int>();
            for (uint i = 0; i < ja.Count; ++i)
            {
                if (ja.GetObjectAt(i).Equals(template))
                {
                    oldidx = (int)i;
                    if (idx == oldidx)
                    {
                        return;
                    }
                }
                JsonObject t = ja.GetObjectAt(i);
                oldidxs.Add(t.GetHashCode(), (int)t.GetNamedNumber("ord"));
                l.Add(t);
            }
            l.RemoveAt(oldidx);
            l.Insert(idx, template);
            m["tmpls"] = l.ToJsonArray();
            UpdateTemplOrds(m);
            // generate change map - We use StringBuilder
            StringBuilder sb = new StringBuilder();
            ja = m.GetNamedArray("tmpls");
            for (uint i = 0; i < ja.Count; ++i)
            {
                JsonObject t = ja.GetObjectAt(i);
                sb.Append("when ord = ");
                sb.Append(oldidxs[t.GetHashCode()]);
                sb.Append(" then ");
                sb.Append(t.GetNamedNumber("ord"));
                if (i != ja.Count - 1)
                {
                    sb.Append(" ");
                }
            }
            // apply
            Save(m);
            collection.Database.Execute("update cards set ord = (case " + sb.ToString() +
                    " end),usn=?,mod=? where nid in (select id from notes where mid = ?)",
                    new object[] { collection.Usn, DateTimeOffset.Now.ToUnixTimeSeconds(), m.GetNamedNumber("id") });
        }

        /// <summary>
        /// Change a model
        /// </summary>
        /// <param name="m">The model to change</param>
        /// <param name="nids">The list of notes that the change applies to</param>
        /// <param name="newModel">For replacing the old model with another one. Should be self if the model is not changing</param>
        /// <param name="fmap">For switching fields. This is ord->ord and there should not be duplicate targets</param>
        /// <param name="cmap">for switching cards. This is ord->ord and there should not be duplicate targets</param>
        public void Change(JsonObject m, long[] nids, JsonObject newModel, Dictionary<int, int?> fmap, Dictionary<int, int?> cmap)
        {
            collection.ModSchema(true);

            Debug.Assert(newModel.GetNamedNumber("id") == m.GetNamedNumber("id") 
                || (fmap != null && cmap != null));
            
            if (fmap != null)
            {
                ChangeNotes(nids, newModel, fmap);
            }
            if (cmap != null)
            {
                ChangeCards(nids, m, newModel, cmap);
            }
            collection.GenCards(nids);
        }

        private void ChangeNotes(long[] nids, JsonObject newModel, Dictionary<int, int?> map)
        {
            List<object[]> objList = new List<object[]>();
            int nfields = newModel.GetNamedArray("flds").Count;
            long mid = (long)newModel.GetNamedNumber("id");

            string sql = "select id, flds from notes where id in " + Utils.Ids2str(nids);
            var list = collection.Database.QueryColumn<NoteTable>(sql);
            foreach(NoteTable note in list)
            {
                long nid = note.Id;
                string[] flds = Utils.SplitFields(note.Fields);
                Dictionary<int, string> newflds = new Dictionary<int, string>();

                foreach (int old in map.Keys)
                {
                    if(map[old] != null)
                    newflds.Add((int)map[old], flds[old]);
                }
                List<string> flds2 = new List<string>();
                for (int c = 0; c < nfields; ++c)
                {
                    if (newflds.ContainsKey(c))
                    {
                        flds2.Add(newflds[c]);
                    }
                    else
                    {
                        flds2.Add("");
                    }
                }
                string joinedFlds = Utils.JoinFields(flds2);
                objList.Add(new object[] { joinedFlds, mid, DateTimeOffset.Now.ToUnixTimeSeconds(), collection.Usn, nid });
            }

            collection.Database.ExecuteMany("update notes set flds=?,mid=?,mod=?,usn=? where id = ?", objList);
            collection.UpdateFieldCache(nids);
        }

        private void ChangeCards(long[] nids, JsonObject oldModel, JsonObject newModel, Dictionary<int, int?> map)
        {
            List<object[]> objList = new List<object[]>();
            List<long> deleted = new List<long>();
            ModelType omType = (ModelType)oldModel.GetNamedNumber("type");
            ModelType nmType = (ModelType)newModel.GetNamedNumber("type");
            int nflds = newModel.GetNamedArray("tmpls").Count;
            string sql = "select id, ord from cards where nid in " + Utils.Ids2str(nids);
            var list = collection.Database.QueryColumn<CardTable>(sql);
            foreach (CardTable c in list)
            {
                // if the src model is a cloze, we ignore the map, as the gui doesn't currently
                // support mapping them
                int? newOrd;
                long cid = c.Id;
                int ord = c.Ord;
                
                if (omType == ModelType.CLOZE)
                {
                    newOrd = c.Ord;
                    if (nmType != ModelType.CLOZE)
                    {
                        // if we're mapping to a regular note, we need to check if
                        // the destination ord is valid
                        if (nflds <= ord)
                        {
                            newOrd = null;
                        }
                    }
                }
                else
                {
                    // mapping from a regular note, so the map should be valid
                    newOrd = map[ord];
                }
                if (newOrd != null)
                {
                    objList.Add(new object[] { newOrd, collection.Usn, DateTimeOffset.Now.ToUnixTimeSeconds(), cid });
                }
                else
                {
                    deleted.Add(cid);
                }
            }
            collection.Database.ExecuteMany("update cards set ord=?,usn=?,mod=? where id=?", objList);
            collection.RemoveCardsAndNoteIfNoCardsLeft(deleted.ToArray());
        }

        /// <summary>
        /// Return a hash of the schema, to see if models are compatible.
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        public string SchemaHash(JsonObject m)
        {
            string s = "";
            JsonArray flds = m.GetNamedArray("flds");
            for (uint i = 0; i < flds.Count; ++i)
            {
                s += flds.GetObjectAt(i).GetNamedString("name");
            }
            JsonArray tmpls = m.GetNamedArray("tmpls");
            for (uint i = 0; i < tmpls.Count; ++i)
            {
                JsonObject t = tmpls.GetObjectAt(i);
                s += t.GetNamedString("name");
            }
            return Utils.Checksum(s);
        }

        /// <summary>
        /// Given a joined field string, return available template ordinals
        /// </summary>
        /// <param name="m"></param>
        /// <param name="flds"></param>
        /// <returns></returns>
        public List<int> AvailableOrds(JsonObject m, string flds)
        {
            bool ok;
            if (m.GetNamedNumber("type") == (double)ModelType.CLOZE)
            {
                return AvailableClozeOrds(m, flds);
            }
            string[] fields = Utils.SplitFields(flds);
            for (int i = 0; i < fields.Length; i++)
            {
                fields[i] = fields[i].Trim();
            }
            List<int> available = new List<int>();
            JsonArray reqArray = m.GetNamedArray("req");
            for (uint i = 0; i < reqArray.Count; i++)
            {
                JsonArray sr = reqArray.GetArrayAt(i);

                int ord = (int)sr.GetNumberAt(0);
                string type = sr.GetStringAt(1);
                JsonArray req = sr.GetArrayAt(2);

                if (type.Equals("none"))
                {
                    // unsatisfiable template
                    continue;
                }
                else if (type.Equals("all"))
                {
                    // AND requirement?
                    ok = true;
                    for (uint j = 0; j < req.Count; j++)
                    {
                        int idx = (int)req.GetNumberAt(j);
                        if (fields[idx] == null || fields[idx].Length == 0)
                        {
                            // missing and was required
                            ok = false;
                            break;
                        }
                    }
                    if (!ok)
                    {
                        continue;
                    }
                }
                else if (type.Equals("any"))
                {
                    // OR requirement?
                    ok = false;
                    for (uint j = 0; j < req.Count; j++)
                    {
                        int idx = (int)req.GetNumberAt(j);
                        if (fields[idx] != null && fields[idx].Length != 0)
                        {
                            // missing and was required
                            ok = true;
                            break;
                        }
                    }
                    if (!ok)
                    {
                        continue;
                    }
                }
                available.Add(ord);
            }
            return available;
        }

        public List<int> AvailableClozeOrds(JsonObject m, string flds, bool allowEmpty = true)
        {
            string[] sflds = Utils.SplitFields(flds);
            Dictionary<string, KeyValuePair<int, JsonObject>> map = FieldMap(m);
            HashSet<int> ords = new HashSet<int>();
            List<string> matches = new List<string>();

            string seachString = m.GetNamedArray("tmpls").GetObjectAt(0).GetNamedString("qfmt");
            AddMatchesIntoList(matches, seachString, fClozePattern1);
            AddMatchesIntoList(matches, seachString, fClozePattern2);

            foreach (string fname in matches)
            {
                if (!map.ContainsKey(fname))
                    continue;
                
                int ord = map[fname].Key;
                MatchCollection matCol = fClozeOrdPattern.Matches(sflds[ord]);
                foreach (Match mm in matCol)
                {
                    ords.Add( int.Parse(mm.GetGroup(1)) - 1);
                }
            }
            if (ords.Contains(-1))
            {
                ords.Remove(-1);
            }
            if ((ords.Count == 0) && (allowEmpty))
            {
                // empty clozes use first ord
                List<int> emptyClozes = new List<int>();
                emptyClozes.Add(0);
                return emptyClozes;
            }
            return new List<int>(ords);
        }

        private void AddMatchesIntoList(List<string> matches, string searchString, Regex pattern)
        {
            MatchCollection matchCol = pattern.Matches(searchString);
            if (matchCol.Count == 0)
                return;
            foreach (Match mm in matchCol)
                matches.Add(mm.GetGroup(1));
        }

        public void BeforeUpload()
        {
            foreach (JsonObject m in All())
                m["usn"] = JsonValue.CreateNumberValue(0);
                
            Save();
        }

        public static JsonObject AddBasicModel(Collection col, string name = "Basic")
        {
            Models mm = col.Models;
            JsonObject m = mm.NewModel(name);
            JsonObject fm = mm.NewField("Front");
            mm.AddField(m, fm);
            fm = mm.NewField("Back");
            mm.AddField(m, fm);
            JsonObject t = mm.NewTemplate("Card 1");
            t["qfmt"] = JsonValue.CreateStringValue("{{Front}}");
            t["afmt"] = JsonValue.CreateStringValue("{{FrontSide}}\n\n<hr id=answer>\n\n{{Back}}");
            mm.AddTemplate(m, t);
            mm.Add(m);
            return m;
        }

        public static JsonObject AddForwardReverse(Collection col)
        {
            string name = "Basic (and reversed card)";
            Models mm = col.Models;
            JsonObject m = AddBasicModel(col);
            m["name"] = JsonValue.CreateStringValue(name);
            JsonObject t = mm.NewTemplate("Card 2");
            t["qfmt"] = JsonValue.CreateStringValue("{{Back}}");
            t["afmt"] = JsonValue.CreateStringValue("{{FrontSide}}\n\n<hr id=answer>\n\n{{Front}}");
            mm.AddTemplate(m, t);
            return m;
        }

        public static JsonObject AddForwardOptionalReverse(Collection col)
        {
            String name = "Basic (optional reversed card)";
            Models mm = col.Models;
            JsonObject m = AddBasicModel(col);
            m["name"] = JsonValue.CreateStringValue(name);
            JsonObject fm = mm.NewField("Add Reverse");
            mm.AddField(m, fm);
            JsonObject t = mm.NewTemplate("Card 2");
            t["qfmt"] = JsonValue.CreateStringValue("{{#Add Reverse}}{{Back}}{{/Add Reverse}}");
            t["afmt"] = JsonValue.CreateStringValue("{{FrontSide}}\n\n<hr id=answer>\n\n{{Front}}");
            mm.AddTemplate(m, t);
            return m;
        }

        public static JsonObject AddClozeModel(Collection col)
        {
            Models mm = col.Models;
            JsonObject m = mm.NewModel("Cloze");        
            m["type"] = JsonValue.CreateNumberValue((double)ModelType.CLOZE);
            string txt = "Text";
            JsonObject fm = mm.NewField(txt);
            mm.AddField(m, fm);
            fm = mm.NewField("Extra");
            mm.AddField(m, fm);
            JsonObject t = mm.NewTemplate("Cloze");
            string fmt = "{{cloze:" + txt + "}}";
            m["css"] = JsonValue.CreateStringValue(
                                m.GetNamedString("css") 
                                + ".cloze {" 
                                + "font-weight: bold;" 
                                + "color: blue;" + "}");

            t["qfmt"] = JsonValue.CreateStringValue(fmt);
            t["afmt"] = JsonValue.CreateStringValue(fmt + "<br>\n{{Extra}}");
            mm.AddTemplate(m, t);
            mm.Add(m);
            return m;
        }

        public void SetChanged()
        {
            isChanged = true;
        }

        public Dictionary<long, Dictionary<int, string>> GetTemplateNames()
        {
            var result = new Dictionary<long, Dictionary<int, string>>();
            foreach (JsonObject m in All())
            {
                JsonArray templates;
                templates = m.GetNamedArray("tmpls");
                Dictionary<int, string> names = new Dictionary<int, string>();
                for (uint i = 0; i < templates.Count; i++)
                {
                    JsonObject t = templates.GetObjectAt(i);
                    names.Add((int)t.GetNamedNumber("ord"), t.GetNamedString("name"));
                }
                result.Add((long)m.GetNamedNumber("id"), names);
            }
            return result;
        }

        /// <summary>
        /// Check if there is a right bracket for every left bracket
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        private bool ValidateBrackets(JsonObject value)
        {
            string s = value.ToString();
            int count = 0;
            bool inQuotes = false;
            char[] ar = s.ToCharArray();
            for (int i = 0; i < ar.Length; i++)
            {
                char c = ar[i];
                // if in quotes, do not count
                if (c == '"' && (i == 0 || (ar[i - 1] != '\\')))
                {
                    inQuotes = !inQuotes;
                    continue;
                }
                if (inQuotes)
                {
                    continue;
                }
                switch (c)
                {
                    case '{':
                        count++;
                        break;
                    case '}':
                        count--;
                        if (count < 0)
                        {
                            return false;
                        }
                        break;
                }
            }
            return (count == 0);
        }

    }

    public class ModelException : Exception
    {
        public ModelException() : base() { }
        public ModelException(string message) : base(message) { }
    }
    
}
            
        
            
    

  
