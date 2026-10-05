namespace OF.UI.Models
{
    public class EditNoteRequest
    {
        public int Id { get; set; }
        public string Key { get; set; }
        public string NoteType { get; set; }
        public string Note { get; set; }
    }
}
