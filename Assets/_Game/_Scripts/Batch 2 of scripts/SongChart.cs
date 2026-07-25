using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The note timeline for one song - a data-only asset, decoupled from any
/// scene or MonoBehaviour. Create one per song via
/// Assets > Create > Rhythm > Song Chart.
/// </summary>
[CreateAssetMenu(fileName = "New Song Chart", menuName = "Rhythm/Song Chart")]
public class SongChart : ScriptableObject {

    [SerializeField] private List<NoteData> notes = new List<NoteData>();
    public IReadOnlyList<NoteData> Notes => notes;

    /// <summary>
    /// Guards against Unity's "Duplicate Array Element" editor action, which
    /// copies a note's serialized data verbatim - including its Id. Also
    /// backfills an Id for any note that somehow ends up without one. Runs
    /// automatically whenever this asset changes in the Inspector.
    /// </summary>
    private void OnValidate() {
        var seenIds = new HashSet<string>();

        foreach (NoteData note in notes) {
            if (note == null) continue;

            if (string.IsNullOrEmpty(note.Id) || seenIds.Contains(note.Id)) {
                note.RegenerateId();
            }

            seenIds.Add(note.Id);
        }
    }
}
