class TransferError(Exception):
    pass

class UserNotFound(TransferError):
    pass

class AccountNotFound(TransferError):
    pass

class AccountBlocked(TransferError):
    pass

class NotEnoughBalance(TransferError):
    pass

class InvalidAmount(TransferError):
    pass

class AmbiguousUsername(TransferError):
    pass

class SelfTransferNotAllowed(TransferError):
    pass
