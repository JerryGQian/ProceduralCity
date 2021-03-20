using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PriorityQueue<T> {
   // Sorts ascending order
   ArrayList queue = new ArrayList();
   ArrayList priorities = new ArrayList();
   HashSet<T> set = new HashSet<T>();

   public void Insert(T obj, float priority) {
      int i = 0;
      foreach (float p in priorities) { 
         if (p > priority) {
            break;
         }
         i++;
      }
      queue.Insert(i, obj);
      priorities.Insert(i, priority);
      set.Add(obj);
   }

   public T PeekFromMin(int idx) {
      if (idx < 0 || queue.Count < idx + 1) {
         return default(T);
      }
      return (T)queue[idx];
   }

   public T PeekFromMax(int idx) {
      int realIdx = queue.Count - idx - 1;
      if (realIdx < 0 || queue.Count < realIdx + 1) {
         return default(T);
      }
      return (T)queue[realIdx];
   }

   public T DequeueMin() {
      if (queue.Count == 0) {
         return default(T);
      }
      return RemoveAt(0);
   }

   public T DequeueMax() {
      if (queue.Count == 0) {
         return default(T);
      }
      return RemoveAt(queue.Count - 1);
   }

   public T RemoveAt(int idx) {
      T obj = (T)queue[idx];
      queue.RemoveAt(idx);
      priorities.RemoveAt(idx);
      set.Remove(obj);
      return obj;
   }

   public bool ContainsKey(T obj) {
      return set.Contains(obj);
   }

   public int Count() {
      return queue.Count;
   }

   public bool HasNext() {
      return queue.Count > 0;
   }
}
