# info.py
TOKEN = '***REMOVED***'

ADMIN_ID = [6120496361]  # ID администраторов

trash_list = ['__class__', '__delattr__', '__dict__', '__dir__', '__doc__', '__eq__', '__format__', '__ge__', '__getattribute__', '__getstate__', '__gt__', 
'__hash__', '__init__', '__init_subclass__', '__le__', '__lt__', '__module__', '__ne__', '__new__', '__reduce__', '__reduce_ex__', '__repr__', '__setattr__', 
'__sizeof__', '__str__', '__subclasshook__', '__weakref__']

def romooover(target, trash):
    for n in trash:
        if hasattr(target, n):
            delattr(target, n)
    return target