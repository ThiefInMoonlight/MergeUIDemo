using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.ComTypes;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MergeUI
{
    public class MergeUIRender : MonoBehaviour
    {
        public Material _material;
        
        public Graphic[] graphicRecords;
        

        protected void Start()
        {
            if(_init)
                Init();
        }

        protected void OnEnable()
        {
            _transform = transform;
        }

        private void OnDisable()
        {
            if(!_init)
                return;
            
            _meshRender.enabled = false;
        }

        protected void OnDestroy()
        {
            if(!_init)
                return;
            
            _meshRender.enabled = false;
        }

        #region Interface

        public void Register(Graphic graphic, IMerge merge)
        {
            InsertSorting(graphic, merge);
            _dirty = true;
        }

        public void UnRegister(Graphic graphic, IMerge merge)
        {
            _graphics.Remove(graphic);
            _merges.Remove(merge);
            _dirty = true;
        }

        public void SetDirty()
        {
            _dirty = true;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UpdateMesh();
#endif
        }

#if UNITY_EDITOR

        public void ReSortingInEditor()
        {
            _merges.Sort((x, y) =>
            {
                var path1 = x.GetPath();
                var path2 = y.GetPath();
                return string.Compare(path1, path2);
            });
            
            _graphics.Clear();
            foreach (var merge in _merges)
            {
                var trans = merge.GetTransform();
                var graphic = trans.GetComponent<Graphic>();
                _graphics.Add(graphic);
            }

            graphicRecords = _graphics.ToArray();
        }
        
#endif

        #endregion

        #region UpdateMesh

        protected void LateUpdate()
        {
            for(int i = 0; i <_merges.Count; i++)
            {
                var merge = _merges[i];
                _dirty = merge.PosCheck(_dirty);
                if(_dirty)
                    break;
            }
            
            if (_dirty)
            {
                UpdateMesh();
                _dirty = false;
            }
        }

        #endregion

        #region Method
        

        private void Init()
        {
            _transform = transform;
            if (_emptyMesh == null)
                _emptyMesh = new Mesh();

            _transform = transform;
            
            _graphics = new List<Graphic>();
            _merges = new List<IMerge>();
            if (graphicRecords == null || graphicRecords.Length == 0)
            {
                Debug.LogError($"[MergeUIRender] Init no merges, pls check");
            }
            
            for (int i = 0; i < graphicRecords.Length; i++)
            {
                var graphic = graphicRecords[i];
                var merge = graphic.gameObject.GetComponent<IMerge>();
                if (graphic == null || merge == null)
                {
                    Debug.LogError($"{gameObject.name} MergeUIRender {i} obj is wrong, pls check");
                    continue;
                }
                
                _graphics.Add(graphic);
                _merges.Add(merge);
            }
            
            _meshRender = gameObject.GetComponent<MeshRenderer>();
            if (_meshRender == null)
                _meshRender = gameObject.AddComponent<MeshRenderer>();
            _meshRender.hideFlags = MeshHideflags;
            _meshRender.sharedMaterial = _material;

            _meshFilter = gameObject.GetComponent<MeshFilter>();
            if (_meshFilter == null)
                _meshFilter = gameObject.AddComponent<MeshFilter>();
            _meshFilter.hideFlags = MeshHideflags;

            _mesh = new Mesh();
            _mesh.MarkDynamic(); 
            _mesh.hideFlags = MeshHideflags;

            _dirty = true;
            _init = true;
        }

        private void UpdateMesh()
        {
            if(!_init)
                Init();
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DrawMeshInPrefabStage();
                return;
            }
#endif
            
            var count = _graphics.Count;
            if (_graphics.Count != _merges.Count)
            {
                Debug.LogError($"[UIMergeRender] grahic count != imerge count, pls check");
                _meshRender.enabled = false;
                return;
            }

            if (count == 0)
            {
                _meshRender.enabled = false;
                return;
            }

            if (_combineInstances == null || _combineInstances.Length != count)
            {
                _combineInstances = new CombineInstance[count];
            }

            var worldToLocalMatrix = _transform.worldToLocalMatrix;
            var enableMeshCount = 0;
            for (int i = 0; i < count; i++)
            {
                var graphic = _graphics[i];
                var imerge = _merges[i];
                var mesh = imerge.GetMesh();
                if (mesh == null)
                {
                    mesh = _emptyMesh;
                }
                else
                {
                    enableMeshCount++;
                }

                _combineInstances[i].mesh = mesh;
                _combineInstances[i].transform = imerge.GetMatrix();//worldToLocalMatrix * imerge.GetMatrix();
            }

            if (enableMeshCount == 0)
            {
                _meshRender.enabled = false;
                return;
            }

            _mesh.CombineMeshes(_combineInstances);
            _meshRender.enabled = true;
            _meshFilter.mesh = _mesh;
        }

        #region SortingOrder

        /// <summary>
        /// Insert Sort
        /// to keep all graphics in order, sort them and save, so don't need to resorting them at runtime
        /// also support runtime resorting, if you need. Use  MergeUIRender.Register(graphic, )
        /// </summary>
        /// <param name="graphic"></param>
        /// <param name="merge"></param>
        private void InsertSorting(Graphic graphic, IMerge merge)
        {
            if (_graphics.Count != _merges.Count)
            {
                Debug.LogError($"[MergeUIRender] sorting failed, _graphics.Count != _merges.Count");
                return;
            }

            if (_graphics.Count == 0)
            {
                _graphics.Add(graphic);
                _merges.Add(merge);
                return;
            }

            var highIndex = 0;
            var lowIndex = 0;
            var midIndex = 0;

            var path = merge.GetPath();
            highIndex = _graphics.Count - 1;
            while (lowIndex < highIndex)
            {
                midIndex = (lowIndex + highIndex) / 2;
                var compareMerge = _merges[midIndex];
                var comparePath = compareMerge.GetPath();
                var result = string.Compare(path, comparePath);
                if (result < 0)
                {
                    highIndex = midIndex - 1;
                }
                else
                {
                    lowIndex = midIndex + 1;
                }
            }

            _graphics.Insert(highIndex, graphic);
            _merges.Insert(highIndex, merge);
        }

        #endregion

#if UNITY_EDITOR

        private void DrawMeshInPrefabStage()
        {
            var count = _graphics.Count;

            for (int i = 0; i < count; i++)
            {
                var graphic = _graphics[i];
                var imerge = _merges[i];

                var mesh = imerge.GetMesh();
                var meshRenderer = imerge.GetTempMeshRenderer();
                var meshFilter = imerge.GetTempMeshFilter();
                var mat = imerge.GetTempMaterial();
                if (mesh == null)
                {
                    meshRenderer.enabled = false;
                    continue;
                }

                meshRenderer.enabled = true;
                meshFilter.mesh = mesh;
                meshRenderer.material = mat;
                meshRenderer.sortingOrder = i;
            }
        }
        
#endif

        #endregion
        
        
        #region Field
        
        [System.NonSerialized]
        private MeshRenderer _meshRender;
        
        [System.NonSerialized]
        private MeshFilter _meshFilter;
        
        [System.NonSerialized]
        private Mesh _mesh;

        private static Mesh _emptyMesh;

        private Transform _transform;

        private bool _dirty;

        private List<Graphic> _graphics = new List<Graphic>();

        private List<IMerge> _merges = new List<IMerge>();

        internal static readonly HideFlags MeshHideflags = 
            HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor | HideFlags.HideInInspector;

        private CombineInstance[] _combineInstances;

        private bool _init = false;

        #endregion
    }
}