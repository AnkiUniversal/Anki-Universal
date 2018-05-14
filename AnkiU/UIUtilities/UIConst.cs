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

namespace AnkiU.UIUtilities
{
    public static class UIConst
    {
        public const string EXPORT_FAILED = "Unable to export the specified deck(s).\n"
                                            + "This might happen if you do not have the permission to write to the chosen folder.";

        public const string CONFIG_INTERVALMOD_HELP = "Due time of a card is calculated by multiplying with this value.\n" + 
                                                      "Decrease it will make review cards appear more frequently";

        public const string CONFIG_BURYNEW_HELP = "After answering a new card, other new cards created from the same note will be hidden until next day.\n" + 
                                                  "This won't affect cards in the review queue.";

        public const string CONFIG_BURYREVIEW_HELP = "After answering a review card, other review cards created from the same note will be hidden until next day.\n" +
                                                     "This won't affect cards in the new queue.";

        public const string CONFIG_LEECHTHRES_HELP = "Number of times a review card is answered as \"AGAIN\" before it is considered as a leech card.";

        public const string DEFAULT_NAME = "Default";
        public const string TAG_ONLY_LEECH_NAME = "Tag On Leech";
        public const string SHORT_INTERVAL_NAME = "Short Interval";
        public const string LONG_INTERVAL_NAME = "Long Interval";
        public const string DUE_ONLY_NAME = "Due Only";

        public const string HELP_TAG_ONLY_LEECH = "Leech cards will only be tag instead of suspending.";
        public const string HELP_SHORT_INTERVAL = "You will see review cards more frequently (next schedule time is decreased to 80%.)";
        public const string HELP_LONG_INTERVAL = "You will see review cards less frequently (next schedule time is increased to 120%.)";
        public const string HELP_DUE_ONLY = "Deck will only show cards you have learned.\n" +
                                            "You can switch back to Default once you want to learn new cards.";

        public const string LEECH_CARD_SUSPEND_CONFIRM = "A leech card is a review card that was marked as \"AGAIN\" too many times (default is 8).\n" +
                                                  "It is best to suspend and forget it, then relearn it in the future.\n" +
                                                  "Do you want suspending to be set as the default action for leech cards of this deck?\n" + 
                                                  "(This message will not be shown again. You can change the default action anytime in deck option)";

        public const string LEECH_CARD_SUSPEND_NOTIFY = "This card is a leech card. It has been marked as \"AGAIN\" too many times (default is 8).\n" +
                                                        "Thus, it will be suspended and removed from your review schedule.\n" +
                                                        "If you want to review it again please choose unsuspend in \"Search Cards\".\n" +
                                                        "If you do not want to suspend leech cards, please change the default behavior in your deck options.";

        public const string NOT_SUSPEND_ACTION_CHOOSE = "All leech cards of this deck will now only be tagged as \"Leech\".\n" +
                                                        "Note that the default action of other decks is still suspending.";        

        public const string MEDIA_BACKUP_TOKEN = "MediaBackupFolder";

        public const string WARN_DELETE_FIELD = "Delete this field will also delete the field in {0} note(s) using this note type. Continue?";
        public const string WARN_NOTSAVE = "You haven't saved yet. Continue and lose all your changes?";
        public const string WARN_NOTE_EXIST = "A note with the same type and first field already exists.\n" +
                                               "Do you want to edit that note and lose current inputs?";
        public const string WARN_NOTETYPE_EXIST = "A note type with the same name already exists. Please enter a different name.";
        public const string WARN_NOTEFIELD_EXIST = "A field with the same name already exists. Please enter a different name.";
        public const string WARN_FULLSYNC = "This will require a full re-upload of your data to anki sever when syncing.\n" +
                                            "If there are pending changes or reviews on other devices, you should sync them first.\n" +
                                            "Otherwise, those information will be lost.\nContinue?";
        public const string WARN_NOTCLOZETYPE = "Please change this note type to cloze in \"Manage Note Types\" first.";
        public const string WARN_NOCLOZE_FIELD = "The template of this note does not contain a cloze field. Please add a cloze field in \"Edit Templates\" first.";
        public const string WARN_NOTEMPLATE_MATCH = "No templates match your current inputs. Please fill in empty fields.";

        public const string WARN_CUSTOM_STUDY_NOCARDS_MATCH = "No cards matched the criteria you provided.";

        public const string WARN_TEMPLATE_DELETE = "Are your sure you want to permanently delete this template and {0} card(s) generated by its?\n"
                                         + "This action cannot be undone. If you re-add this template you will have to re-learn its cards.";

        public const string CAN_NOT_DELETE__FIELD = "One note type needs at least two fields.";

        public const double SMALEST_SCREEN_WIDTH = 320; //Used to test smallest device screen only
        public const double SMALEST_SCREEN_HEIGHT = 533;
    }

    public enum SearchSortColumn
    {
        SortField,
        Question,
        Answer,
        Due,
        Lapse,
        TimeCreated,
        TimeModified      
    }
}
