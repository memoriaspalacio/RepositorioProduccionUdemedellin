using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UdeM.Base;

namespace UdeM.Sounds
{
    public class SoundManager : CustomMonoBehaviour
    {
        [SerializeField] protected AudioSource _audioSource;
        [SerializeField] protected Dictionary<string, AudioClip> _audioClips;
        protected List<AudioClip> _clips;
        protected int _iterator;
        protected bool _isLooping;

        protected override void Awake()
        {
            base.Awake();
            _clips = new List<AudioClip>();
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioClips = new Dictionary<string, AudioClip>();
            _isLooping = false;
        }

        public void PlayClips(List<string> clipKeys, int replay = 1, float delay = 0, float pitch = 1)
        {
            if (!KeysExits(clipKeys)) { return; }
            _iterator = 0;
            _clips.Clear();
            foreach (string clipKey in clipKeys) {
                _clips.Add(_audioClips[clipKey]);
            }
            StartCoroutine(Play(replay, delay, pitch));
        }

        public void PlayClip(string clipKey, int replay = 1, float delay = 0, float pitch = 1)
        {
            if (!KeyExits(clipKey)) {  return; }
            _iterator = 0;
            _clips.Clear();
            _clips.Add(_audioClips[clipKey]);
            StartCoroutine(Play(replay, delay, pitch));
        }

        private IEnumerator Play(int replay, float delay, float pitch)
        {
            _audioSource.pitch = pitch;
            _audioSource.PlayOneShot(_clips[_iterator]);
            yield return new WaitUntil(() => { return !_audioSource.isPlaying; });
            yield return new WaitForSeconds(delay);
            if(_iterator >= _clips.Count - 1) {
                _iterator = 0;
                replay--;
            } else {
                _iterator++;
            }
            if(replay > 0) {
                StartCoroutine(Play(replay, delay, pitch));
            }
        }

        public void PlayLoopClip(string clipKey, float delay = 0, float pitch = 1)
        {
            if (!KeyExits(clipKey) || _audioSource.isPlaying || _isLooping) { return; }
            _isLooping = true;
            _audioSource.pitch = pitch;
            StartCoroutine(PlayLoop(_audioClips[clipKey], delay, pitch));
        }
        private IEnumerator PlayLoop(AudioClip clip, float delay, float pitch)
        {
            _audioSource.PlayOneShot(clip);
            yield return new WaitForSeconds(delay + clip.length);
            StartCoroutine(PlayLoop(clip, delay, pitch));
        }
        private bool KeyExits(string key)
        {
            if (!_audioClips.ContainsKey(key)) {
                Debug.LogWarning($"The AudioClip {key} not don't exists");
                return false;
            }
            return true;
        }
        private bool KeysExits(List<string> keys)
        {
            foreach (string key in keys) {
                if (!_audioClips.ContainsKey(key)) {
                    Debug.LogWarning($"The AudioClip {key} not don't exists");
                    return false;
                }
            }
            return true;
        }

        public void StopLoop()
        {
            StopAllCoroutines();
            _audioSource.Stop();
            _isLooping = false;
        }

        public void AddClips(string path, List<string> clipsUrl)
        {
            foreach(var cl in clipsUrl) {
                AddClip(path, cl);
            }
        }

        public void AddClip(string path, string clipUrl)
        {
            AudioClip ac = Resources.Load<AudioClip>(path + clipUrl);
            if(ac == null) {
                Debug.LogWarning("The resource clip not exists: " + path + clipUrl);
            }
            _audioClips.Add(clipUrl, ac);
        }
    }
}