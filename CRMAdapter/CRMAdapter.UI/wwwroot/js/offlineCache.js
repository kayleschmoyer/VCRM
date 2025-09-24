const dbName = 'crmAdapterOffline';
const storeName = 'entries';
let dbPromise = null;
let connectivityHandlers = null;

function openDb() {
    if (!dbPromise) {
        dbPromise = new Promise((resolve, reject) => {
            const request = indexedDB.open(dbName, 1);
            request.onupgradeneeded = () => {
                const db = request.result;
                if (!db.objectStoreNames.contains(storeName)) {
                    db.createObjectStore(storeName, { keyPath: 'id' });
                }
            };
            request.onerror = () => reject(request.error);
            request.onsuccess = () => resolve(request.result);
        });
    }

    return dbPromise;
}

async function withStore(mode) {
    const db = await openDb();
    const transaction = db.transaction(storeName, mode);
    return transaction.objectStore(storeName);
}

function compositeKey(typeKey, key) {
    return `${typeKey}::${key}`;
}

export async function setEntry(typeKey, key, jsonPayload) {
    const store = await withStore('readwrite');
    return new Promise((resolve, reject) => {
        const request = store.put({ id: compositeKey(typeKey, key), typeKey, json: jsonPayload });
        request.onsuccess = () => resolve();
        request.onerror = () => reject(request.error);
    });
}

export async function getEntry(typeKey, key) {
    const store = await withStore('readonly');
    return new Promise((resolve, reject) => {
        const request = store.get(compositeKey(typeKey, key));
        request.onsuccess = () => resolve(request.result ? request.result.json : null);
        request.onerror = () => reject(request.error);
    });
}

export async function deleteEntry(typeKey, key) {
    const store = await withStore('readwrite');
    return new Promise((resolve, reject) => {
        const request = store.delete(compositeKey(typeKey, key));
        request.onsuccess = () => resolve();
        request.onerror = () => reject(request.error);
    });
}

export async function getAllEntries(typeKey) {
    const store = await withStore('readonly');
    return new Promise((resolve, reject) => {
        const items = [];
        const request = store.openCursor();
        request.onsuccess = event => {
            const cursor = event.target.result;
            if (cursor) {
                if (cursor.value.typeKey === typeKey) {
                    items.push(cursor.value.json);
                }
                cursor.continue();
            } else {
                resolve(items);
            }
        };
        request.onerror = () => reject(request.error);
    });
}

export async function registerConnectivity(dotnetRef) {
    if (!dotnetRef) {
        return;
    }

    const notify = () => dotnetRef.invokeMethodAsync('UpdateOnlineStatus', navigator.onLine);
    notify();

    const onlineHandler = () => notify();
    const offlineHandler = () => notify();
    window.addEventListener('online', onlineHandler);
    window.addEventListener('offline', offlineHandler);
    connectivityHandlers = { onlineHandler, offlineHandler };
}

export async function disposeConnectivity() {
    if (connectivityHandlers) {
        window.removeEventListener('online', connectivityHandlers.onlineHandler);
        window.removeEventListener('offline', connectivityHandlers.offlineHandler);
        connectivityHandlers = null;
    }
}
